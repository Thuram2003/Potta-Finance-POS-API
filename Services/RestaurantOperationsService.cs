using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PottaAPI.Models;
using System.Text.Json;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for restaurant operations (add notes, transfer server, move orders, etc.)
    /// </summary>
    public class RestaurantOperationsService : IRestaurantOperationsService
    {
        private readonly DatabaseService _databaseService;
        private readonly IOrderService _orderService;
        private readonly IStaffService _staffService;
        private readonly ITableService _tableService;
        private readonly string _connectionString;

        public RestaurantOperationsService(
            DatabaseService databaseService,
            IOrderService orderService,
            IStaffService staffService,
            ITableService tableService)
        {
            _databaseService = databaseService;
            _orderService = orderService;
            _staffService = staffService;
            _tableService = tableService;
            _connectionString = _databaseService.GetConnectionString();
        }

        #region Add Notes

        /// <summary>
        /// Add notes to an order/transaction
        /// </summary>
        public async Task<AddNotesResponse> AddNotesAsync(AddNotesRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Get staff info if staffId provided
            string notePrefix = "";
            if (request.AddedByStaffId.HasValue && request.AddedByStaffId.Value > 0)
            {
                var staff = await _staffService.GetStaffByIdAsync(request.AddedByStaffId.Value);
                if (staff != null)
                {
                    notePrefix = $"[{staff.FirstName} {staff.LastName} - {DateTime.Now:HH:mm}] ";
                }
            }

            // 3. Get existing notes
            string existingNotes = transaction.Notes ?? string.Empty;

            // 4. Append new note with staff info and timestamp
            string formattedNote = $"{notePrefix}{request.NoteText}";
            string updatedNotes = string.IsNullOrEmpty(existingNotes)
                ? formattedNote
                : $"{existingNotes}\n{formattedNote}";

            // 5. Update database
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE WaitingTransactions 
                    SET Notes = @notes,
                        ModifiedDate = @modifiedDate
                    WHERE TransactionId = @transactionId";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@notes", updatedNotes);
                    command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                    command.Parameters.AddWithValue("@transactionId", request.TransactionId);

                    await command.ExecuteNonQueryAsync();
                }
            }

            return new AddNotesResponse
            {
                TransactionId = request.TransactionId,
                NoteText = request.NoteText,
                AddedAt = DateTime.Now,
                Message = "Note added successfully"
            };
        }

        #endregion

        #region Transfer Server

        /// <summary>
        /// Transfer an order to a different server/staff
        /// </summary>
        public async Task<TransferServerResponse> TransferServerAsync(TransferServerRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Validate new staff exists and is active
            var newStaff = await _staffService.GetStaffByIdAsync(request.NewStaffId);
            if (newStaff == null)
            {
                throw new KeyNotFoundException($"Staff member {request.NewStaffId} not found");
            }

            if (!newStaff.IsActive)
            {
                throw new InvalidOperationException($"Staff member {newStaff.FirstName} {newStaff.LastName} is not active");
            }

            // 3. Get previous staff info (if any)
            StaffDTO? previousStaff = null;
            if (transaction.StaffId.HasValue)
            {
                previousStaff = await _staffService.GetStaffByIdAsync(transaction.StaffId.Value);
            }

            // 4. Update transaction StaffId
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE WaitingTransactions 
                    SET StaffId = @newStaffId,
                        ModifiedDate = @modifiedDate
                    WHERE TransactionId = @transactionId";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@newStaffId", request.NewStaffId);
                    command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                    command.Parameters.AddWithValue("@transactionId", request.TransactionId);

                    await command.ExecuteNonQueryAsync();
                }

                // 5. Update CartItems JSON to change StaffId in each item
                var cartItems = transaction.Items;
                if (cartItems != null && cartItems.Count > 0)
                {
                    foreach (var item in cartItems)
                    {
                        item.StaffId = request.NewStaffId;
                    }

                    var updatedCartItemsJson = JsonConvert.SerializeObject(cartItems);

                    sql = @"
                        UPDATE WaitingTransactions 
                        SET CartItems = @cartItems
                        WHERE TransactionId = @transactionId";

                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@cartItems", updatedCartItemsJson);
                        command.Parameters.AddWithValue("@transactionId", request.TransactionId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }

            return new TransferServerResponse
            {
                TransactionId = request.TransactionId,
                PreviousStaffId = transaction.StaffId,
                PreviousStaffName = previousStaff != null ? $"{previousStaff.FirstName} {previousStaff.LastName}" : "None",
                NewStaffId = request.NewStaffId,
                NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
                TransferredAt = DateTime.Now,
                Message = $"Order transferred from {(previousStaff != null ? $"{previousStaff.FirstName} {previousStaff.LastName}" : "unassigned")} to {newStaff.FirstName} {newStaff.LastName}"
            };
        }

        #endregion

        #region Shift Handover

        /// <summary>
        /// Transfer all orders from one staff to another (shift handover)
        /// </summary>
        public async Task<ShiftHandoverResponse> ShiftHandoverAsync(ShiftHandoverRequest request)
        {
            // 1. Validate both staff members exist and are active
            var currentStaff = await _staffService.GetStaffByIdAsync(request.CurrentStaffId);
            if (currentStaff == null)
            {
                throw new KeyNotFoundException($"Current staff member {request.CurrentStaffId} not found");
            }

            var newStaff = await _staffService.GetStaffByIdAsync(request.NewStaffId);
            if (newStaff == null)
            {
                throw new KeyNotFoundException($"New staff member {request.NewStaffId} not found");
            }

            if (!newStaff.IsActive)
            {
                throw new InvalidOperationException($"New staff member {newStaff.FirstName} {newStaff.LastName} is not active");
            }

            // 2. Get all orders for current staff
            var allOrders = await _orderService.GetWaitingTransactionsAsync();
            var staffOrders = allOrders.Where(o => o.StaffId == request.CurrentStaffId).ToList();

            if (staffOrders.Count == 0)
            {
                return new ShiftHandoverResponse
                {
                    CurrentStaffId = request.CurrentStaffId,
                    CurrentStaffName = $"{currentStaff.FirstName} {currentStaff.LastName}",
                    NewStaffId = request.NewStaffId,
                    NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
                    OrdersTransferred = 0,
                    TransferredTransactionIds = new List<string>(),
                    HandoverAt = DateTime.Now,
                    Message = "No orders to transfer"
                };
            }

            // 3. Transfer all orders
            var transferredIds = new List<string>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                foreach (var order in staffOrders)
                {
                    // Update transaction StaffId
                    var sql = @"
                        UPDATE WaitingTransactions 
                        SET StaffId = @newStaffId,
                            ModifiedDate = @modifiedDate
                        WHERE TransactionId = @transactionId";

                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@newStaffId", request.NewStaffId);
                        command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                        command.Parameters.AddWithValue("@transactionId", order.TransactionId);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Update CartItems JSON
                    var cartItems = order.Items;
                    if (cartItems != null && cartItems.Count > 0)
                    {
                        foreach (var item in cartItems)
                        {
                            item.StaffId = request.NewStaffId;
                        }

                        var updatedCartItemsJson = JsonConvert.SerializeObject(cartItems);

                        sql = @"
                            UPDATE WaitingTransactions 
                            SET CartItems = @cartItems
                            WHERE TransactionId = @transactionId";

                        using (var command = new SqliteCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@cartItems", updatedCartItemsJson);
                            command.Parameters.AddWithValue("@transactionId", order.TransactionId);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    transferredIds.Add(order.TransactionId);
                }
            }

            return new ShiftHandoverResponse
            {
                CurrentStaffId = request.CurrentStaffId,
                CurrentStaffName = $"{currentStaff.FirstName} {currentStaff.LastName}",
                NewStaffId = request.NewStaffId,
                NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
                OrdersTransferred = transferredIds.Count,
                TransferredTransactionIds = transferredIds,
                HandoverAt = DateTime.Now,
                Message = $"Successfully transferred {transferredIds.Count} order(s) from {currentStaff.FirstName} to {newStaff.FirstName}"
            };
        }

        #endregion

        #region Move Order

        /// <summary>
        /// Move an order from one table to another
        /// </summary>
        public async Task<MoveOrderResponse> MoveOrderAsync(MoveOrderRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Get source and target tables
            var sourceTableId = transaction.TableId;
            TableDTO? sourceTable = null;
            if (!string.IsNullOrEmpty(sourceTableId))
            {
                sourceTable = await _tableService.GetTableByIdAsync(sourceTableId);
            }

            var targetTable = await _tableService.GetTableByIdAsync(request.TargetTableId);
            if (targetTable == null)
            {
                throw new KeyNotFoundException($"Target table {request.TargetTableId} not found");
            }

            // 3. Check if target table is already occupied
            if (targetTable.Status == "Occupied")
            {
                throw new InvalidOperationException($"Target table {targetTable.TableName} is already occupied. Please select an available table.");
            }

            // 4. Check if target table has any occupied seats
            var hasOccupiedSeats = await _tableService.AnySeatsOccupiedAsync(request.TargetTableId);
            if (hasOccupiedSeats)
            {
                throw new InvalidOperationException($"Target table {targetTable.TableName} has occupied seats. Please free all seats before moving order.");
            }

            // 5. Update transaction with new table info
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE WaitingTransactions 
                    SET TableId = @targetTableId,
                        TableNumber = @targetTableNumber,
                        TableName = @targetTableName,
                        ModifiedDate = @modifiedDate
                    WHERE TransactionId = @transactionId";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@targetTableId", targetTable.TableId);
                    command.Parameters.AddWithValue("@targetTableNumber", targetTable.TableNumber);
                    command.Parameters.AddWithValue("@targetTableName", targetTable.TableName ?? "");
                    command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                    command.Parameters.AddWithValue("@transactionId", request.TransactionId);

                    await command.ExecuteNonQueryAsync();
                }
            }

            // 6. Update table statuses
            if (sourceTable != null)
            {
                await _tableService.UpdateTableStatusAsync(sourceTable.TableId, new UpdateTableStatusDTO 
                { 
                    Status = "Available",
                    CustomerId = null,
                    TransactionId = null
                });
            }

            await _tableService.UpdateTableStatusAsync(targetTable.TableId, new UpdateTableStatusDTO
            {
                Status = "Occupied",
                CustomerId = transaction.CustomerId,
                TransactionId = request.TransactionId
            });

            return new MoveOrderResponse
            {
                TransactionId = request.TransactionId,
                FromTableId = sourceTableId,
                FromTableName = sourceTable?.TableName ?? "Unknown",
                ToTableId = targetTable.TableId,
                ToTableName = targetTable.TableName ?? "",
                MovedAt = DateTime.Now,
                Message = $"Order moved from {sourceTable?.TableName ?? "Unknown"} to {targetTable.TableName}"
            };
        }

        #endregion

        #region Print Bill Operations

        /// <summary>
        /// Create a print bill request for desktop to process.
        /// Idempotent: returns existing Pending request if one exists for the same transaction.
        /// Blocks if a PayEntireBillRequest is already pending for the same transaction.
        /// </summary>
        public async Task<PrintBillResponse> CreatePrintBillRequestAsync(PrintBillRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Validate staff exists
            var staff = await _staffService.GetStaffByIdAsync(request.StaffId);
            if (staff == null)
            {
                throw new KeyNotFoundException($"Staff member {request.StaffId} not found");
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 3. Check if a PayEntireBillRequest is already pending for this transaction
                var conflictSql = @"
                    SELECT COUNT(1) FROM PayEntireBillRequests
                    WHERE transactionId = @transactionId AND status = 'Pending'";

                using (var cmd = new SqliteCommand(conflictSql, connection))
                {
                    cmd.Parameters.AddWithValue("@transactionId", request.TransactionId);
                    var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        throw new InvalidOperationException(
                            $"A payment request is already pending for transaction {request.TransactionId}. " +
                            "Cannot create a print-bill request at the same time.");
                    }
                }

                // 4. Check if a PrintBillRequest is already pending for this transaction (idempotency)
                var existingSql = @"
                    SELECT requestId, transactionId, staffId, staffName, tableId, tableName,
                           requestedAt, status, notes
                    FROM PrintBillRequests
                    WHERE transactionId = @transactionId AND status = 'Pending'
                    LIMIT 1";

                using (var cmd = new SqliteCommand(existingSql, connection))
                {
                    cmd.Parameters.AddWithValue("@transactionId", request.TransactionId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        // Return existing request instead of creating a duplicate
                        return new PrintBillResponse
                        {
                            RequestId = reader.GetString(0),
                            TransactionId = reader.GetString(1),
                            StaffName = reader.GetString(3),
                            TableName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            RequestedAt = reader.GetDateTime(6),
                            Status = reader.GetString(7),
                            Message = "Existing pending print bill request returned (no duplicate created)"
                        };
                    }
                }

                // 5. Generate new request ID and insert
                var requestId = $"PBR-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";

                var sql = @"
                    INSERT INTO PrintBillRequests 
                    (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, notes)
                    VALUES 
                    (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @notes)";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@requestId", requestId);
                    command.Parameters.AddWithValue("@transactionId", request.TransactionId);
                    command.Parameters.AddWithValue("@staffId", request.StaffId);
                    command.Parameters.AddWithValue("@staffName", $"{staff.FirstName} {staff.LastName}");
                    command.Parameters.AddWithValue("@tableId", (object?)transaction.TableId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@tableName", (object?)transaction.TableName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@requestedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@status", "Pending");
                    command.Parameters.AddWithValue("@notes", (object?)request.Notes ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }

                return new PrintBillResponse
                {
                    RequestId = requestId,
                    TransactionId = request.TransactionId,
                    StaffName = $"{staff.FirstName} {staff.LastName}",
                    TableName = transaction.TableName,
                    RequestedAt = DateTime.Now,
                    Status = "Pending",
                    Message = "Print bill request created successfully"
                };
            }
        }

        /// <summary>
        /// Create print bill requests for ALL open orders on a specific table.
        /// One PrintBillRequest is created per open order. The desktop desktop polls 
        /// GetPendingPrintBillRequests and receives all of them, then shows a single
        /// combined dialog to approve printing all bills at once.
        /// </summary>
        public async Task<PrintBillByTableResponse> CreatePrintBillByTableRequestAsync(PrintBillByTableRequest request)
        {
            // 1. Validate staff exists
            var staff = await _staffService.GetStaffByIdAsync(request.StaffId);
            if (staff == null)
            {
                throw new KeyNotFoundException($"Staff member {request.StaffId} not found");
            }

            // 2. Validate table exists
            var table = await _tableService.GetTableByIdAsync(request.TableId);
            if (table == null)
            {
                throw new KeyNotFoundException($"Table {request.TableId} not found");
            }

            // 3. Get all open (waiting) transactions for this table
            var allTransactions = await _orderService.GetWaitingTransactionsAsync();
            var tableTransactions = allTransactions
                .Where(t => t.TableId == request.TableId)
                .ToList();

            if (tableTransactions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No open orders found for table {table.TableName ?? request.TableId}");
            }

            var createdRequestIds = new List<string>();
            var staffFullName = $"{staff.FirstName} {staff.LastName}";

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                foreach (var transaction in tableTransactions)
                {
                    // Check if a PayEntireBillRequest is already pending for this transaction — skip if so
                    var conflictSql = @"
                        SELECT COUNT(1) FROM PayEntireBillRequests
                        WHERE transactionId = @transactionId AND status = 'Pending'";

                    using (var conflictCmd = new SqliteCommand(conflictSql, connection))
                    {
                        conflictCmd.Parameters.AddWithValue("@transactionId", transaction.TransactionId);
                        var cnt = Convert.ToInt64(await conflictCmd.ExecuteScalarAsync());
                        if (cnt > 0) continue; // skip — payment already pending
                    }

                    // Check if a PrintBillRequest is already pending (idempotency per transaction)
                    var existingSql = @"
                        SELECT requestId FROM PrintBillRequests
                        WHERE transactionId = @transactionId AND status = 'Pending'
                        LIMIT 1";

                    string? existingRequestId = null;
                    using (var existCmd = new SqliteCommand(existingSql, connection))
                    {
                        existCmd.Parameters.AddWithValue("@transactionId", transaction.TransactionId);
                        var result = await existCmd.ExecuteScalarAsync();
                        existingRequestId = result as string;
                    }

                    if (existingRequestId != null)
                    {
                        // Reuse the existing request — don't create a duplicate
                        createdRequestIds.Add(existingRequestId);
                        continue;
                    }

                    // Create a new request for this transaction
                    var requestId = $"PBR-{DateTime.Now:yyyyMMddHHmmssff}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";

                    var insertSql = @"
                        INSERT INTO PrintBillRequests 
                        (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, notes)
                        VALUES 
                        (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @notes)";

                    using (var insertCmd = new SqliteCommand(insertSql, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@requestId", requestId);
                        insertCmd.Parameters.AddWithValue("@transactionId", transaction.TransactionId);
                        insertCmd.Parameters.AddWithValue("@staffId", request.StaffId);
                        insertCmd.Parameters.AddWithValue("@staffName", staffFullName);
                        insertCmd.Parameters.AddWithValue("@tableId", (object?)transaction.TableId ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@tableName", (object?)transaction.TableName ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@requestedAt", DateTime.Now);
                        insertCmd.Parameters.AddWithValue("@status", "Pending");
                        insertCmd.Parameters.AddWithValue("@notes", (object?)request.Notes ?? DBNull.Value);

                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    createdRequestIds.Add(requestId);
                }
            }

            return new PrintBillByTableResponse
            {
                RequestCount = createdRequestIds.Count,
                TableId = request.TableId,
                TableName = table.TableName,
                RequestIds = createdRequestIds,
                Message = createdRequestIds.Count == 0
                    ? "No new print requests created (payment requests already pending for all orders)"
                    : $"Created {createdRequestIds.Count} print bill request(s) for {table.TableName ?? request.TableId}"
            };
        }

        /// <summary>
        /// Get all pending print bill requests (for desktop polling)
        /// </summary>
        public async Task<List<PrintBillRequestDTO>> GetPendingPrintBillRequestsAsync()
        {
            var requests = new List<PrintBillRequestDTO>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    SELECT requestId, transactionId, staffId, staffName, tableId, tableName, 
                           requestedAt, status, notes
                    FROM PrintBillRequests
                    WHERE status = 'Pending'
                    ORDER BY requestedAt ASC";

                using (var command = new SqliteCommand(sql, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        requests.Add(new PrintBillRequestDTO
                        {
                            RequestId = reader.GetString(0),
                            TransactionId = reader.GetString(1),
                            StaffId = reader.GetInt32(2),
                            StaffName = reader.GetString(3),
                            TableId = reader.IsDBNull(4) ? null : reader.GetString(4),
                            TableName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            RequestedAt = reader.GetDateTime(6),
                            Status = reader.GetString(7),
                            Notes = reader.IsDBNull(8) ? null : reader.GetString(8)
                        });
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Mark a print bill request as completed
        /// </summary>
        public async Task<bool> CompletePrintBillRequestAsync(string requestId, string? completedBy)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE PrintBillRequests
                    SET status = 'Completed',
                        completedAt = @completedAt,
                        completedBy = @completedBy
                    WHERE requestId = @requestId AND status = 'Pending'";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@completedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@completedBy", (object?)completedBy ?? DBNull.Value);
                    command.Parameters.AddWithValue("@requestId", requestId);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        /// <summary>
        /// Cancel a print bill request
        /// </summary>
        public async Task<bool> CancelPrintBillRequestAsync(string requestId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE PrintBillRequests
                    SET status = 'Cancelled'
                    WHERE requestId = @requestId AND status = 'Pending'";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@requestId", requestId);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        #endregion


        #region Pay Entire Bill Operations

        /// <summary>
        /// Create a pay entire bill request (mobile staff requests desktop to complete payment)
        /// </summary>
        public async Task<PayEntireBillResponse> CreatePayEntireBillRequestAsync(PayEntireBillRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Validate staff exists
            var staff = await _staffService.GetStaffByIdAsync(request.StaffId);
            if (staff == null)
            {
                throw new KeyNotFoundException($"Staff member {request.StaffId} not found");
            }

            // 3. Generate request ID
            var requestId = $"PEBR-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";

            // 4. Insert pay entire bill request
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    INSERT INTO PayEntireBillRequests 
                    (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, notes)
                    VALUES 
                    (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @notes)";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@requestId", requestId);
                    command.Parameters.AddWithValue("@transactionId", request.TransactionId);
                    command.Parameters.AddWithValue("@staffId", request.StaffId);
                    command.Parameters.AddWithValue("@staffName", $"{staff.FirstName} {staff.LastName}");
                    command.Parameters.AddWithValue("@tableId", (object?)transaction.TableId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@tableName", (object?)transaction.TableName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@requestedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@status", "Pending");
                    command.Parameters.AddWithValue("@notes", (object?)request.Notes ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }
            }

            return new PayEntireBillResponse
            {
                RequestId = requestId,
                TransactionId = request.TransactionId,
                StaffName = $"{staff.FirstName} {staff.LastName}",
                TableName = transaction.TableName,
                RequestedAt = DateTime.Now,
                Status = "Pending",
                Message = "Pay entire bill request created successfully"
            };
        }

        /// <summary>
        /// Get all pending pay entire bill requests (desktop polls this)
        /// </summary>
        public async Task<List<PayEntireBillRequestDTO>> GetPendingPayEntireBillRequestsAsync()
        {
            var requests = new List<PayEntireBillRequestDTO>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    SELECT requestId, transactionId, staffId, staffName, tableId, tableName, 
                           requestedAt, status, notes
                    FROM PayEntireBillRequests
                    WHERE status = 'Pending'
                    ORDER BY requestedAt ASC";

                using (var command = new SqliteCommand(sql, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        requests.Add(new PayEntireBillRequestDTO
                        {
                            RequestId = reader.GetString(0),
                            TransactionId = reader.GetString(1),
                            StaffId = reader.GetInt32(2),
                            StaffName = reader.GetString(3),
                            TableId = reader.IsDBNull(4) ? null : reader.GetString(4),
                            TableName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            RequestedAt = reader.GetDateTime(6),
                            Status = reader.GetString(7),
                            Notes = reader.IsDBNull(8) ? null : reader.GetString(8)
                        });
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Complete a pay entire bill request (desktop marks as completed after payment)
        /// </summary>
        public async Task<bool> CompletePayEntireBillRequestAsync(string requestId, string? completedBy)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE PayEntireBillRequests
                    SET status = 'Completed',
                        completedAt = @completedAt,
                        completedBy = @completedBy
                    WHERE requestId = @requestId AND status = 'Pending'";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@completedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@completedBy", (object?)completedBy ?? DBNull.Value);
                    command.Parameters.AddWithValue("@requestId", requestId);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        /// <summary>
        /// Cancel a pay entire bill request
        /// </summary>
        public async Task<bool> CancelPayEntireBillRequestAsync(string requestId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE PayEntireBillRequests
                    SET status = 'Cancelled'
                    WHERE requestId = @requestId AND status = 'Pending'";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@requestId", requestId);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        #endregion

        #region Refire To Kitchen Operations

        /// <summary>
        /// Mark an order as refired (updates WaitingTransaction)
        /// </summary>
        public async Task<RefireToKitchenResponse> RefireToKitchenAsync(RefireToKitchenRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Validate staff exists
            var staff = await _staffService.GetStaffByIdAsync(request.StaffId);
            if (staff == null)
            {
                throw new KeyNotFoundException($"Staff member {request.StaffId} not found");
            }

            // 3. Get cart items
            var cartItems = transaction.Items;
            if (cartItems == null || cartItems.Count == 0)
            {
                throw new InvalidOperationException("Transaction has no items");
            }

            // 4. Determine which items to refire
            List<WaitingTransactionItemDto> itemsToRefire;
            if (request.ItemIndices == null || request.ItemIndices.Count == 0)
            {
                // Refire all items
                itemsToRefire = cartItems;
            }
            else
            {
                // Refire specific items
                itemsToRefire = new List<WaitingTransactionItemDto>();
                foreach (var index in request.ItemIndices)
                {
                    if (index < 0 || index >= cartItems.Count)
                    {
                        throw new ArgumentException($"Invalid item index: {index}");
                    }
                    itemsToRefire.Add(cartItems[index]);
                }
            }

            // 5. Update WaitingTransaction with refire info
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var sql = @"
                    UPDATE WaitingTransactions 
                    SET IsRefired = 1,
                        RefireReason = @refireReason,
                        RefiredAt = @refiredAt,
                        RefiredByStaffId = @refiredByStaffId,
                        RefiredByStaffName = @refiredByStaffName,
                        ModifiedDate = @modifiedDate
                    WHERE TransactionId = @transactionId";

                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@refireReason", request.Reason);
                    command.Parameters.AddWithValue("@refiredAt", DateTime.Now);
                    command.Parameters.AddWithValue("@refiredByStaffId", request.StaffId);
                    command.Parameters.AddWithValue("@refiredByStaffName", $"{staff.FirstName} {staff.LastName}");
                    command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                    command.Parameters.AddWithValue("@transactionId", request.TransactionId);

                    await command.ExecuteNonQueryAsync();
                }
            }

            return new RefireToKitchenResponse
            {
                TransactionId = request.TransactionId,
                ItemsRefired = itemsToRefire.Count,
                RefiredAt = DateTime.Now,
                Message = $"Order marked as refired. {itemsToRefire.Count} item(s) will be reprinted."
            };
        }

        #endregion

        #region Combine Orders Operations

        /// <summary>
        /// Combine multiple orders into one
        /// </summary>
        public async Task<CombineOrdersResponse> CombineOrdersAsync(CombineOrdersRequest request)
        {
            // 1. Remove duplicate transaction IDs
            var uniqueTransactionIds = request.TransactionIds.Distinct().ToList();
            
            if (uniqueTransactionIds.Count < 2)
            {
                throw new ArgumentException("At least 2 unique transactions are required to combine orders");
            }

            // 2. Validate all transactions exist
            var transactions = new List<WaitingTransactionDto>();
            foreach (var id in uniqueTransactionIds)
            {
                var transaction = await _orderService.GetWaitingTransactionByIdAsync(id);
                if (transaction == null)
                {
                    throw new KeyNotFoundException($"Transaction {id} not found");
                }
                transactions.Add(transaction);
            }

            // 3. Validate target table exists
            var targetTable = await _tableService.GetTableByIdAsync(request.TargetTableId);
            if (targetTable == null)
            {
                throw new KeyNotFoundException($"Target table {request.TargetTableId} not found");
            }

            // 4. Validate target staff exists and is active
            var targetStaff = await _staffService.GetStaffByIdAsync(request.TargetStaffId);
            if (targetStaff == null)
            {
                throw new KeyNotFoundException($"Target staff {request.TargetStaffId} not found");
            }

            if (!targetStaff.IsActive)
            {
                throw new InvalidOperationException($"Target staff {targetStaff.FirstName} {targetStaff.LastName} is not active");
            }

            // 5. Merge all cart items
            var allCartItems = new List<WaitingTransactionItemDto>();
            foreach (var transaction in transactions)
            {
                if (transaction.Items != null && transaction.Items.Count > 0)
                {
                    allCartItems.AddRange(transaction.Items);
                }
            }

            // 6. Merge duplicate items (same product, modifiers, price)
            var mergedItems = MergeCartItems(allCartItems);

            // 7. Calculate totals
            decimal totalAmount = mergedItems.Sum(i => (i.Price * i.Quantity) + i.TaxAmount);

            // 8. Create new combined transaction
            var newTransactionId = $"M{DateTime.Now:yyyyMMddHHmmss}";
            var cartItemsJson = JsonConvert.SerializeObject(mergedItems);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert new combined transaction
                        var sql = @"
                            INSERT INTO WaitingTransactions 
                            (TransactionId, CartItems, CustomerId, TableId, TableNumber, TableName, 
                             Status, StaffId, Notes, CreatedDate, ModifiedDate)
                            VALUES 
                            (@transactionId, @cartItems, @customerId, @tableId, @tableNumber, @tableName,
                             @status, @staffId, @notes, @createdDate, @modifiedDate)";

                        using (var command = new SqliteCommand(sql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@transactionId", newTransactionId);
                            command.Parameters.AddWithValue("@cartItems", cartItemsJson);
                            command.Parameters.AddWithValue("@customerId", (object?)transactions[0].CustomerId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@tableId", request.TargetTableId);
                            command.Parameters.AddWithValue("@tableNumber", targetTable.TableNumber);
                            command.Parameters.AddWithValue("@tableName", targetTable.TableName ?? "");
                            command.Parameters.AddWithValue("@status", "Pending");
                            command.Parameters.AddWithValue("@staffId", request.TargetStaffId);
                            command.Parameters.AddWithValue("@notes", (object?)request.Notes ?? DBNull.Value);
                            command.Parameters.AddWithValue("@createdDate", DateTime.Now);
                            command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);

                            await command.ExecuteNonQueryAsync();
                        }

                        // Delete related records first (foreign key constraints)
                        foreach (var id in uniqueTransactionIds)
                        {
                            // Delete print bill requests
                            sql = "DELETE FROM PrintBillRequests WHERE transactionId = @transactionId";
                            using (var command = new SqliteCommand(sql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@transactionId", id);
                                await command.ExecuteNonQueryAsync();
                            }

                            // Delete pay entire bill requests
                            sql = "DELETE FROM PayEntireBillRequests WHERE transactionId = @transactionId";
                            using (var command = new SqliteCommand(sql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@transactionId", id);
                                await command.ExecuteNonQueryAsync();
                            }

                            // Delete tax adjustment audit logs
                            sql = "DELETE FROM TaxAdjustmentAuditLog WHERE transactionId = @transactionId";
                            using (var command = new SqliteCommand(sql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@transactionId", id);
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        // Now delete original transactions
                        foreach (var id in uniqueTransactionIds)
                        {
                            sql = "DELETE FROM WaitingTransactions WHERE TransactionId = @transactionId";
                            using (var command = new SqliteCommand(sql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@transactionId", id);
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        // Update target table status
                        sql = @"
                            UPDATE Tables 
                            SET status = 'Occupied',
                                currentTransactionId = @transactionId
                            WHERE tableId = @tableId";

                        using (var command = new SqliteCommand(sql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@transactionId", newTransactionId);
                            command.Parameters.AddWithValue("@tableId", request.TargetTableId);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // Calculate merged items count
            int mergedItemsCount = allCartItems.Count - mergedItems.Count;

            return new CombineOrdersResponse
            {
                NewTransactionId = newTransactionId,
                CombinedFromIds = uniqueTransactionIds,
                TotalItems = mergedItems.Count,
                TotalAmount = totalAmount,
                MergedItemsCount = mergedItemsCount,
                Timestamp = DateTime.Now,
                Message = $"Successfully combined {uniqueTransactionIds.Count} orders into 1 (merged {mergedItemsCount} duplicate items)"
            };
        }

        /// <summary>
        /// Merge duplicate cart items (same product, modifiers, price)
        /// </summary>
        private List<WaitingTransactionItemDto> MergeCartItems(List<WaitingTransactionItemDto> items)
        {
            var mergedItems = new List<WaitingTransactionItemDto>();

            foreach (var item in items)
            {
                // Find existing item that can be merged
                var existingItem = mergedItems.FirstOrDefault(m => CanMergeItems(m, item));

                if (existingItem != null)
                {
                    // Merge: sum quantities
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    // Add as new item
                    mergedItems.Add(new WaitingTransactionItemDto
                    {
                        ProductId = item.ProductId,
                        Name = item.Name,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        AppliedModifiers = item.AppliedModifiers,
                        TaxId = item.TaxId,
                        Taxable = item.Taxable,
                        TaxAmount = item.TaxAmount,
                        StaffId = item.StaffId
                    });
                }
            }

            return mergedItems;
        }

        /// <summary>
        /// Check if two items can be merged (same product, modifiers, price)
        /// </summary>
        private bool CanMergeItems(WaitingTransactionItemDto item1, WaitingTransactionItemDto item2)
        {
            // Must have same product ID, name, price, tax settings
            if (item1.ProductId != item2.ProductId ||
                item1.Name != item2.Name ||
                item1.Price != item2.Price ||
                item1.TaxId != item2.TaxId ||
                item1.Taxable != item2.Taxable)
            {
                return false;
            }

            // Compare modifiers (JSON comparison)
            var modifiers1Json = GetModifiersJson(item1.AppliedModifiers);
            var modifiers2Json = GetModifiersJson(item2.AppliedModifiers);

            return modifiers1Json == modifiers2Json;
        }

        /// <summary>
        /// Get normalized JSON string for modifiers comparison
        /// </summary>
        private string GetModifiersJson(List<AppliedModifierDto>? modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return "[]";
            }

            // Serialize to JSON for comparison
            return JsonConvert.SerializeObject(modifiers);
        }

        #endregion

        #region Remove Taxes and Fees Operations

        /// <summary>
        /// Remove taxes and fees from an order
        /// </summary>
        public async Task<RemoveTaxesAndFeesResponse> RemoveTaxesAndFeesAsync(RemoveTaxesAndFeesRequest request)
        {
            // 1. Validate transaction exists
            var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");
            }

            // 2. Validate staff exists
            var staff = await _staffService.GetStaffByIdAsync(request.StaffId);
            if (staff == null)
            {
                throw new KeyNotFoundException($"Staff member {request.StaffId} not found");
            }

            // 3. Get cart items
            var cartItems = transaction.Items;
            if (cartItems == null || cartItems.Count == 0)
            {
                throw new InvalidOperationException("Transaction has no items");
            }

            // 4. Calculate original tax amount
            decimal originalTaxAmount = cartItems.Sum(i => i.TaxAmount);

            // 5. Remove taxes from all items
            int itemsAffected = 0;
            foreach (var item in cartItems)
            {
                if (item.TaxAmount > 0)
                {
                    item.TaxAmount = 0;
                    item.Taxable = false;
                    itemsAffected++;
                }
            }

            // 6. Update transaction
            var updatedCartItemsJson = JsonConvert.SerializeObject(cartItems);
            var auditLogId = $"AUDIT-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var dbTransaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Update WaitingTransactions
                        var sql = @"
                            UPDATE WaitingTransactions 
                            SET CartItems = @cartItems,
                                ModifiedDate = @modifiedDate
                            WHERE TransactionId = @transactionId";

                        using (var command = new SqliteCommand(sql, connection, dbTransaction))
                        {
                            command.Parameters.AddWithValue("@cartItems", updatedCartItemsJson);
                            command.Parameters.AddWithValue("@modifiedDate", DateTime.Now);
                            command.Parameters.AddWithValue("@transactionId", request.TransactionId);

                            await command.ExecuteNonQueryAsync();
                        }

                        // Create audit log entry
                        sql = @"
                            INSERT INTO TaxAdjustmentAuditLog 
                            (auditId, transactionId, staffId, staffName, action, applyTo, 
                             originalTaxAmount, newTaxAmount, reason, timestamp)
                            VALUES 
                            (@auditId, @transactionId, @staffId, @staffName, @action, @applyTo,
                             @originalTaxAmount, @newTaxAmount, @reason, @timestamp)";

                        using (var command = new SqliteCommand(sql, connection, dbTransaction))
                        {
                            command.Parameters.AddWithValue("@auditId", auditLogId);
                            command.Parameters.AddWithValue("@transactionId", request.TransactionId);
                            command.Parameters.AddWithValue("@staffId", request.StaffId);
                            command.Parameters.AddWithValue("@staffName", $"{staff.FirstName} {staff.LastName}");
                            command.Parameters.AddWithValue("@action", "Remove");
                            command.Parameters.AddWithValue("@applyTo", "Order");
                            command.Parameters.AddWithValue("@originalTaxAmount", originalTaxAmount);
                            command.Parameters.AddWithValue("@newTaxAmount", 0);
                            command.Parameters.AddWithValue("@reason", request.Reason);
                            command.Parameters.AddWithValue("@timestamp", DateTime.Now);

                            await command.ExecuteNonQueryAsync();
                        }

                        dbTransaction.Commit();
                    }
                    catch
                    {
                        dbTransaction.Rollback();
                        throw;
                    }
                }
            }

            return new RemoveTaxesAndFeesResponse
            {
                TransactionId = request.TransactionId,
                OriginalTaxAmount = originalTaxAmount,
                TaxRemoved = originalTaxAmount,
                ItemsAffected = itemsAffected,
                RemovedBy = $"{staff.FirstName} {staff.LastName}",
                AuditLogId = auditLogId,
                Timestamp = DateTime.Now,
                Message = $"Taxes and fees removed successfully. {itemsAffected} item(s) affected."
            };
        }

        #endregion
    }
}
