using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services;

public class OrderOperationsService : IOrderOperationsService
{
    private readonly string _connectionString;
    private readonly IOrderService _orderService;
    private readonly IStaffService _staffService;
    private readonly ITableService _tableService;

    public OrderOperationsService(
        IConnectionStringProvider connectionStringProvider,
        IOrderService orderService,
        IStaffService staffService,
        ITableService tableService)
    {
        _connectionString = connectionStringProvider.GetConnectionString();
        _orderService = orderService;
        _staffService = staffService;
        _tableService = tableService;
    }

    public async Task<AddNotesResponse> AddNotesAsync(AddNotesRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        string notePrefix = "";
        if (request.AddedByStaffId.HasValue && request.AddedByStaffId.Value > 0)
        {
            var staff = await _staffService.GetStaffByIdAsync(request.AddedByStaffId.Value);
            if (staff != null)
                notePrefix = $"[{staff.FirstName} {staff.LastName} - {DateTime.Now:HH:mm}] ";
        }

        string updatedNotes = string.IsNullOrEmpty(transaction.Notes)
            ? $"{notePrefix}{request.NoteText}"
            : $"{transaction.Notes}\n{notePrefix}{request.NoteText}";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET Notes = @notes, ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new { notes = updatedNotes, modifiedDate = DateTime.Now, transactionId = request.TransactionId });

        return new AddNotesResponse
        {
            TransactionId = request.TransactionId,
            NoteText = request.NoteText,
            AddedAt = DateTime.Now,
            Message = "Note added successfully"
        };
    }

    public async Task<TransferServerResponse> TransferServerAsync(TransferServerRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var newStaff = await _staffService.GetStaffByIdAsync(request.NewStaffId)
            ?? throw new KeyNotFoundException($"Staff {request.NewStaffId} not found");

        if (!newStaff.IsActive)
            throw new InvalidOperationException($"Staff {newStaff.FirstName} {newStaff.LastName} is not active");

        StaffDTO? previousStaff = null;
        if (transaction.StaffId.HasValue)
            previousStaff = await _staffService.GetStaffByIdAsync(transaction.StaffId.Value);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET StaffId = @newStaffId, ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new { newStaffId = request.NewStaffId, modifiedDate = DateTime.Now, transactionId = request.TransactionId });

        if (transaction.Items?.Count > 0)
        {
            foreach (var item in transaction.Items)
                item.StaffId = request.NewStaffId;

            var json = JsonConvert.SerializeObject(transaction.Items);
            await connection.ExecuteAsync(@"
                UPDATE WaitingTransactions SET CartItems = @cartItems WHERE TransactionId = @transactionId",
                new { cartItems = json, transactionId = request.TransactionId });
        }

        return new TransferServerResponse
        {
            TransactionId = request.TransactionId,
            PreviousStaffId = transaction.StaffId,
            PreviousStaffName = previousStaff != null ? $"{previousStaff.FirstName} {previousStaff.LastName}" : "None",
            NewStaffId = request.NewStaffId,
            NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
            TransferredAt = DateTime.Now,
            Message = $"Order transferred to {newStaff.FirstName} {newStaff.LastName}"
        };
    }

    public async Task<ShiftHandoverResponse> ShiftHandoverAsync(ShiftHandoverRequest request)
    {
        var currentStaff = await _staffService.GetStaffByIdAsync(request.CurrentStaffId)
            ?? throw new KeyNotFoundException($"Current staff {request.CurrentStaffId} not found");

        var newStaff = await _staffService.GetStaffByIdAsync(request.NewStaffId)
            ?? throw new KeyNotFoundException($"New staff {request.NewStaffId} not found");

        if (!newStaff.IsActive)
            throw new InvalidOperationException($"New staff {newStaff.FirstName} {newStaff.LastName} is not active");

        var allOrders = await _orderService.GetWaitingTransactionsAsync();
        var staffOrders = allOrders.Where(o => o.StaffId == request.CurrentStaffId).ToList();

        if (staffOrders.Count == 0)
            return new ShiftHandoverResponse
            {
                CurrentStaffId = request.CurrentStaffId,
                CurrentStaffName = $"{currentStaff.FirstName} {currentStaff.LastName}",
                NewStaffId = request.NewStaffId,
                NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
                OrdersTransferred = 0,
                Message = "No orders to transfer"
            };

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var order in staffOrders)
        {
            await connection.ExecuteAsync(@"
                UPDATE WaitingTransactions SET StaffId = @newStaffId, ModifiedDate = @modifiedDate
                WHERE TransactionId = @transactionId",
                new { newStaffId = request.NewStaffId, modifiedDate = DateTime.Now, transactionId = order.TransactionId });

            if (order.Items?.Count > 0)
            {
                foreach (var item in order.Items) item.StaffId = request.NewStaffId;
                var json = JsonConvert.SerializeObject(order.Items);
                await connection.ExecuteAsync(@"
                    UPDATE WaitingTransactions SET CartItems = @cartItems WHERE TransactionId = @transactionId",
                    new { cartItems = json, transactionId = order.TransactionId });
            }
        }

        return new ShiftHandoverResponse
        {
            CurrentStaffId = request.CurrentStaffId,
            CurrentStaffName = $"{currentStaff.FirstName} {currentStaff.LastName}",
            NewStaffId = request.NewStaffId,
            NewStaffName = $"{newStaff.FirstName} {newStaff.LastName}",
            OrdersTransferred = staffOrders.Count,
            TransferredTransactionIds = staffOrders.Select(o => o.TransactionId).ToList(),
            HandoverAt = DateTime.Now,
            Message = $"Transferred {staffOrders.Count} order(s)"
        };
    }

    public async Task<MoveOrderResponse> MoveOrderAsync(MoveOrderRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var sourceTableId = transaction.TableId;
        var sourceTable = !string.IsNullOrEmpty(sourceTableId)
            ? await _tableService.GetTableByIdAsync(sourceTableId) : null;

        var targetTable = await _tableService.GetTableByIdAsync(request.TargetTableId)
            ?? throw new KeyNotFoundException($"Target table {request.TargetTableId} not found");

        // Removed occupancy validation - allow moving to any table for now

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET TableId = @targetTableId, TableNumber = @targetTableNumber, 
                TableName = @targetTableName, ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new
            {
                targetTableId = targetTable.TableId,
                targetTableNumber = targetTable.TableNumber,
                targetTableName = targetTable.TableName ?? "",
                modifiedDate = DateTime.Now,
                transactionId = request.TransactionId
            });

        // Set source table to Available only if it exists
        if (sourceTable != null)
            await _tableService.UpdateTableStatusAsync(sourceTable.TableId, new UpdateTableStatusDTO
            { Status = "Available", CustomerId = null, TransactionId = null });

        // Do NOT update target table status - keep it as Available for now
        // This allows multiple orders to move to the same table without validation errors

        return new MoveOrderResponse
        {
            TransactionId = request.TransactionId,
            FromTableId = sourceTableId,
            FromTableName = sourceTable?.TableName ?? "Unknown",
            ToTableId = targetTable.TableId,
            ToTableName = targetTable.TableName ?? "",
            MovedAt = DateTime.Now,
            Message = $"Order moved to {targetTable.TableName}"
        };
    }

    public async Task<RefireToKitchenResponse> RefireToKitchenAsync(RefireToKitchenRequest request)
    {
        var transaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        var cartItems = transaction.Items;
        if (cartItems == null || cartItems.Count == 0)
            throw new InvalidOperationException("Transaction has no items");

        List<WaitingTransactionItemDto> itemsToRefire;
        if (request.ItemIndices == null || request.ItemIndices.Count == 0)
        {
            itemsToRefire = cartItems;
        }
        else
        {
            itemsToRefire = new List<WaitingTransactionItemDto>();
            foreach (var index in request.ItemIndices)
            {
                if (index < 0 || index >= cartItems.Count)
                    throw new ArgumentException($"Invalid item index: {index}");
                itemsToRefire.Add(cartItems[index]);
            }
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            UPDATE WaitingTransactions 
            SET IsRefired = 1, RefireReason = @refireReason, RefiredAt = @refiredAt,
                RefiredByStaffId = @refiredByStaffId, RefiredByStaffName = @refiredByStaffName,
                ModifiedDate = @modifiedDate
            WHERE TransactionId = @transactionId",
            new
            {
                refireReason = request.Reason,
                refiredAt = DateTime.Now,
                refiredByStaffId = request.StaffId,
                refiredByStaffName = $"{staff.FirstName} {staff.LastName}",
                modifiedDate = DateTime.Now,
                transactionId = request.TransactionId
            });

        return new RefireToKitchenResponse
        {
            TransactionId = request.TransactionId,
            ItemsRefired = itemsToRefire.Count,
            RefiredAt = DateTime.Now,
            Message = $"Order marked as refired. {itemsToRefire.Count} item(s) will be reprinted."
        };
    }

    public async Task<CombineOrdersResponse> CombineOrdersAsync(CombineOrdersRequest request)
    {
        var uniqueIds = request.TransactionIds.Distinct().ToList();
        if (uniqueIds.Count < 2)
            throw new ArgumentException("At least 2 unique transactions required");

        var transactions = new List<WaitingTransactionDto>();
        foreach (var id in uniqueIds)
        {
            var t = await _orderService.GetWaitingTransactionByIdAsync(id)
                ?? throw new KeyNotFoundException($"Transaction {id} not found");
            transactions.Add(t);
        }

        var targetTable = await _tableService.GetTableByIdAsync(request.TargetTableId)
            ?? throw new KeyNotFoundException($"Target table {request.TargetTableId} not found");

        var targetStaff = await _staffService.GetStaffByIdAsync(request.TargetStaffId)
            ?? throw new KeyNotFoundException($"Target staff {request.TargetStaffId} not found");

        if (!targetStaff.IsActive)
            throw new InvalidOperationException($"Target staff {targetStaff.FirstName} {targetStaff.LastName} is not active");

        var allCartItems = transactions
            .Where(t => t.Items != null)
            .SelectMany(t => t.Items!)
            .ToList();

        var mergedItems = MergeCartItems(allCartItems);
        var totalAmount = mergedItems.Sum(i => (i.Price * i.Quantity) + i.TaxAmount);
        var newTransactionId = $"M{DateTime.Now:yyyyMMddHHmmss}";
        var cartItemsJson = JsonConvert.SerializeObject(mergedItems);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO WaitingTransactions 
                (TransactionId, CartItems, CustomerId, TableId, TableNumber, TableName, 
                 Status, StaffId, Notes, CreatedDate, ModifiedDate)
                VALUES 
                (@transactionId, @cartItems, @customerId, @tableId, @tableNumber, @tableName,
                 @status, @staffId, @notes, @createdDate, @modifiedDate)",
                new
                {
                    transactionId = newTransactionId,
                    cartItems = cartItemsJson,
                    customerId = (object?)transactions[0].CustomerId ?? DBNull.Value,
                    tableId = request.TargetTableId,
                    tableNumber = targetTable.TableNumber,
                    tableName = targetTable.TableName ?? "",
                    status = "Pending",
                    staffId = request.TargetStaffId,
                    notes = (object?)request.Notes ?? DBNull.Value,
                    createdDate = DateTime.Now,
                    modifiedDate = DateTime.Now
                }, dbTransaction);

            foreach (var id in uniqueIds)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM PrintBillRequests WHERE transactionId = @id",
                    new { id }, dbTransaction);
                await connection.ExecuteAsync(
                    "DELETE FROM PayEntireBillRequests WHERE transactionId = @id",
                    new { id }, dbTransaction);
                await connection.ExecuteAsync(
                    "DELETE FROM TaxAdjustmentAuditLog WHERE transactionId = @id",
                    new { id }, dbTransaction);
                await connection.ExecuteAsync(
                    "DELETE FROM WaitingTransactions WHERE TransactionId = @id",
                    new { id }, dbTransaction);
            }

            await connection.ExecuteAsync(@"
                UPDATE Tables SET status = 'Occupied', currentTransactionId = @transactionId
                WHERE tableId = @tableId",
                new { transactionId = newTransactionId, tableId = request.TargetTableId }, dbTransaction);

            dbTransaction.Commit();
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }

        return new CombineOrdersResponse
        {
            NewTransactionId = newTransactionId,
            CombinedFromIds = uniqueIds,
            TotalItems = mergedItems.Count,
            TotalAmount = totalAmount,
            MergedItemsCount = allCartItems.Count - mergedItems.Count,
            Timestamp = DateTime.Now,
            Message = $"Combined {uniqueIds.Count} orders into 1"
        };
    }

    private List<WaitingTransactionItemDto> MergeCartItems(List<WaitingTransactionItemDto> items)
    {
        var merged = new List<WaitingTransactionItemDto>();
        foreach (var item in items)
        {
            var existing = merged.FirstOrDefault(m => CanMergeItems(m, item));
            if (existing != null)
                existing.Quantity += item.Quantity;
            else
                merged.Add(new WaitingTransactionItemDto
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
        return merged;
    }

    private bool CanMergeItems(WaitingTransactionItemDto a, WaitingTransactionItemDto b)
    {
        if (a.ProductId != b.ProductId || a.Name != b.Name || a.Price != b.Price ||
            a.TaxId != b.TaxId || a.Taxable != b.Taxable)
            return false;

        return JsonConvert.SerializeObject(a.AppliedModifiers) == JsonConvert.SerializeObject(b.AppliedModifiers);
    }

    public async Task<MobileCompletePaymentResponse> MobileCompletePaymentAsync(MobileCompletePaymentRequest request)
    {
        // 1. Get the waiting transaction
        var waitingTransaction = await _orderService.GetWaitingTransactionByIdAsync(request.TransactionId)
            ?? throw new KeyNotFoundException($"Transaction {request.TransactionId} not found");

        // 2. Get staff info
        var staff = await _staffService.GetStaffByIdAsync(request.StaffId)
            ?? throw new KeyNotFoundException($"Staff {request.StaffId} not found");

        // 3. Validate payment amount matches transaction total
        if (Math.Abs(request.Amount - waitingTransaction.TotalAmount) > 0.01m)
            throw new InvalidOperationException(
                $"Payment amount ({request.Amount:N2}) does not match transaction total ({waitingTransaction.TotalAmount:N2})");

        // 4. Validate payment method
        var validPaymentMethods = new[] { "Cash", "MTN Mobile Money", "Orange Money" };
        if (!validPaymentMethods.Contains(request.PaymentMethod))
            throw new ArgumentException($"Invalid payment method. Must be one of: {string.Join(", ", validPaymentMethods)}");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            // 5. Validate customerId exists in Customer table if not null
            object? validCustomerId = DBNull.Value;
            if (!string.IsNullOrEmpty(waitingTransaction.CustomerId))
            {
                var customerExists = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM Customer WHERE customerId = @customerId",
                    new { customerId = waitingTransaction.CustomerId }, dbTransaction);
                validCustomerId = customerExists > 0 ? (object)waitingTransaction.CustomerId : DBNull.Value;
            }
            
            // 6. Insert into Transactions table
            await connection.ExecuteAsync(@"
                INSERT INTO Transactions 
                (transactionId, customerId, totalAmount, taxAmount, paymentMethod, status, 
                 transactionDate, orderId, TableId, SeatIds, createdBy, updatedBy, createdDate, modifiedDate)
                VALUES 
                (@transactionId, @customerId, @totalAmount, @taxAmount, @paymentMethod, @status,
                 @transactionDate, @orderId, @tableId, @seatIds, @createdBy, @updatedBy, @createdDate, @modifiedDate)",
                new
                {
                    transactionId = request.TransactionId,
                    customerId = validCustomerId,
                    totalAmount = waitingTransaction.TotalAmount,
                    taxAmount = waitingTransaction.Items.Sum(i => i.TaxAmount), // Calculate tax from items
                    paymentMethod = request.PaymentMethod,
                    status = "Completed",
                    transactionDate = DateTime.Now,
                    orderId = request.TransactionId,
                    tableId = (object?)waitingTransaction.TableId ?? DBNull.Value,
                    seatIds = (object?)waitingTransaction.SeatIds ?? DBNull.Value, // Use actual SeatIds from waiting transaction
                    createdBy = $"Staff_{request.StaffId}",
                    updatedBy = $"Staff_{request.StaffId}",
                    createdDate = DateTime.Now,
                    modifiedDate = DateTime.Now
                }, dbTransaction);

            // 6. Insert into TransactionPayments table
            await connection.ExecuteAsync(@"
                INSERT INTO TransactionPayments 
                (transactionId, paymentMethod, amount, reference, paymentDate, createdBy, updatedBy)
                VALUES 
                (@transactionId, @paymentMethod, @amount, @reference, @paymentDate, @createdBy, @updatedBy)",
                new
                {
                    transactionId = request.TransactionId,
                    paymentMethod = request.PaymentMethod,
                    amount = request.Amount,
                    reference = (object?)request.Reference ?? DBNull.Value,
                    paymentDate = DateTime.Now,
                    createdBy = $"Staff_{request.StaffId}",
                    updatedBy = $"Staff_{request.StaffId}"
                }, dbTransaction);

            // 7. Insert transaction items
            if (waitingTransaction.Items != null && waitingTransaction.Items.Count > 0)
            {
                foreach (var item in waitingTransaction.Items)
                {
                    var totalItemPrice = item.Price * item.Quantity;
                    
                    // Validate foreign keys exist or set to NULL to avoid constraint violations
                    object? validTaxId = DBNull.Value;
                    if (!string.IsNullOrEmpty(item.TaxId))
                    {
                        var taxExists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(*) FROM Taxes WHERE taxId = @taxId",
                            new { taxId = item.TaxId }, dbTransaction);
                        validTaxId = taxExists > 0 ? (object)item.TaxId : DBNull.Value;
                    }
                    
                    object? validStaffId = DBNull.Value;
                    if (item.StaffId.HasValue)
                    {
                        var staffExists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(*) FROM Staff WHERE Id = @staffId",
                            new { staffId = item.StaffId.Value }, dbTransaction);
                        validStaffId = staffExists > 0 ? (object)item.StaffId.Value : DBNull.Value;
                    }
                    
                    await connection.ExecuteAsync(@"
                        INSERT INTO TransactionItems 
                        (transactionId, itemId, itemType, productName, quantity, price, totalItemPrice, 
                         taxAmount, taxable, taxId, Modifiers, staffId, createdDate, modifiedDate, createdBy, updatedBy)
                        VALUES 
                        (@transactionId, @itemId, @itemType, @productName, @quantity, @price, @totalItemPrice,
                         @taxAmount, @taxable, @taxId, @modifiers, @staffId, @createdDate, @modifiedDate, @createdBy, @updatedBy)",
                        new
                        {
                            transactionId = request.TransactionId,
                            itemId = item.ProductId,
                            itemType = "Product",
                            productName = item.Name,
                            quantity = item.Quantity,
                            price = item.Price,
                            totalItemPrice = totalItemPrice,
                            taxAmount = item.TaxAmount,
                            taxable = item.Taxable,
                            taxId = validTaxId,
                            modifiers = item.AppliedModifiers != null 
                                ? JsonConvert.SerializeObject(item.AppliedModifiers) 
                                : null,
                            staffId = validStaffId,
                            createdDate = DateTime.Now,
                            modifiedDate = DateTime.Now,
                            createdBy = $"Staff_{request.StaffId}",
                            updatedBy = $"Staff_{request.StaffId}"
                        }, dbTransaction);
                }
            }

            // 8. Delete from WaitingTransactions
            await connection.ExecuteAsync(
                "DELETE FROM WaitingTransactions WHERE TransactionId = @transactionId",
                new { transactionId = request.TransactionId }, dbTransaction);

            // 9. Update table status if applicable
            if (!string.IsNullOrEmpty(waitingTransaction.TableId))
            {
                await connection.ExecuteAsync(@"
                    UPDATE Tables 
                    SET status = 'Available', currentTransactionId = NULL, currentCustomerId = NULL
                    WHERE tableId = @tableId",
                    new { tableId = waitingTransaction.TableId }, dbTransaction);
            }

            // 10. Clean up any pending bill requests for this transaction
            await connection.ExecuteAsync(
                "DELETE FROM PrintBillRequests WHERE transactionId = @transactionId AND status = 'Pending'",
                new { transactionId = request.TransactionId }, dbTransaction);
            await connection.ExecuteAsync(
                "DELETE FROM PayEntireBillRequests WHERE transactionId = @transactionId AND status = 'Pending'",
                new { transactionId = request.TransactionId }, dbTransaction);

            // 11. Create print receipt request so desktop automatically prints the receipt.
            // transactionId now references the permanent Transactions table (no FK to WaitingTransactions).
            // STATUS: "Pending" allows desktop to poll and process it
            // isPrintReceipt: true tells desktop to load from Transactions (not WaitingTransactions)
            await connection.ExecuteAsync(@"
                INSERT INTO PrintBillRequests 
                (requestId, transactionId, staffId, staffName, tableId, tableName, requestedAt, status, isPrintReceipt, completedBy, completedAt)
                VALUES 
                (@requestId, @transactionId, @staffId, @staffName, @tableId, @tableName, @requestedAt, @status, @isPrintReceipt, @completedBy, @completedAt)",
                new
                {
                    requestId = Guid.NewGuid().ToString(),
                    transactionId = request.TransactionId,
                    staffId = request.StaffId,
                    staffName = $"{staff.FirstName} {staff.LastName}",
                    tableId = waitingTransaction.TableId,
                    tableName = waitingTransaction.TableName,
                    requestedAt = DateTime.Now,
                    status = "Pending", // Desktop will poll for "Pending" requests
                    isPrintReceipt = true, // true = load from Transactions, false = load from WaitingTransactions
                    completedBy = (int?)null, // Will be set when desktop completes it
                    completedAt = (DateTime?)null
                }, dbTransaction);

            dbTransaction.Commit();

            return new MobileCompletePaymentResponse
            {
                TransactionId = request.TransactionId,
                PaymentMethod = request.PaymentMethod,
                Amount = request.Amount,
                Reference = request.Reference,
                CompletedAt = DateTime.Now,
                StaffName = $"{staff.FirstName} {staff.LastName}",
                Message = $"Payment completed successfully via {request.PaymentMethod}"
            };
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }
    }
}