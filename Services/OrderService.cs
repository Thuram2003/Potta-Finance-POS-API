using Dapper;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;
using System.Collections.Generic;
using System.Text.Json;

namespace PottaAPI.Services
{
    public class OrderService : IOrderService
    {
        private readonly string _connectionString;
        private readonly ITaxService? _taxService;

        public OrderService(IConnectionStringProvider connectionStringProvider, ITaxService? taxService = null)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _taxService = taxService;
        }

        public async Task<string> CreateWaitingTransactionAsync(CreateWaitingTransactionDto transaction)
        {
            try
            {
                if (_taxService != null)
                {
                    await _taxService.UpdateOrderItemTaxesAsync(transaction.Items);
                    Console.WriteLine($"✅ Taxes calculated for {transaction.Items.Count} items");
                }

                var transactionId = "M" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(1000, 9999);

                var sql = @"
                    INSERT INTO WaitingTransactions (
                        TransactionId, CartItems, CustomerId, TableId, TableNumber, 
                        TableName, SeatIds, StaffId, Notes, CreatedDate, ModifiedDate, Status,
                        IsRefired, RefireReason, RefiredAt, RefiredByStaffId, RefiredByStaffName, RefiredItemIndices,
                        createdBy, updatedBy
                    ) 
                    VALUES (
                        @TransactionId, @CartItems, @CustomerId, @TableId, @TableNumber, 
                        @TableName, @SeatIds, @StaffId, @Notes, @CreatedDate, @ModifiedDate, @Status,
                        @IsRefired, @RefireReason, @RefiredAt, @RefiredByStaffId, @RefiredByStaffName, @RefiredItemIndices,
                        @createdBy, @updatedBy
                    )";

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = null
                };
                
                var cartItemsJson = JsonSerializer.Serialize(transaction.Items, jsonOptions);
                var auditUser = $"Staff_{transaction.StaffId}";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var dbTransaction = connection.BeginTransaction();

                try
                {
                    int result = await connection.ExecuteAsync(sql, new
                    {
                        TransactionId = transactionId,
                        CartItems = cartItemsJson,
                        CustomerId = (object?)transaction.CustomerId ?? DBNull.Value,
                        TableId = (object?)transaction.TableId ?? DBNull.Value,
                        TableNumber = transaction.TableNumber.HasValue ? (object)transaction.TableNumber.Value : DBNull.Value,
                        TableName = (object?)transaction.TableName ?? DBNull.Value,
                        SeatIds = (object?)transaction.SeatIds ?? DBNull.Value,
                        StaffId = transaction.StaffId,
                        Notes = (object?)transaction.Notes ?? DBNull.Value,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        Status = "Pending",
                        IsRefired = false,
                        RefireReason = (object?)null ?? DBNull.Value,
                        RefiredAt = (object?)null ?? DBNull.Value,
                        RefiredByStaffId = (object?)null ?? DBNull.Value,
                        RefiredByStaffName = (object?)null ?? DBNull.Value,
                        RefiredItemIndices = (object?)null ?? DBNull.Value,
                        createdBy = auditUser,
                        updatedBy = auditUser
                    }, dbTransaction);

                    // Auto-update seat statuses if seatIds were provided.
                    // This means the mobile only needs to call POST /api/orders 
                    // no separate seat status calls required.
                    if (!string.IsNullOrEmpty(transaction.SeatIds))
                    {
                        var seatIdList = transaction.SeatIds
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        if (seatIdList.Length > 0)
                        {
                            await connection.ExecuteAsync(@"
                                UPDATE Seats 
                                SET status = 'Occupied', modifiedDate = @modifiedDate
                                WHERE seatId IN @seatIds",
                                new { seatIds = seatIdList, modifiedDate = DateTime.UtcNow },
                                dbTransaction);

                            Console.WriteLine($"✅ Auto-marked {seatIdList.Length} seat(s) as Occupied for order {transactionId}");
                        }
                    }

                    dbTransaction.Commit();
                    Console.WriteLine($"✅ Waiting transaction created (ID: {transactionId}). Rows affected: {result}");
                    return transactionId;
                }
                catch
                {
                    dbTransaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating waiting transaction: {ex.Message}");
                throw new Exception($"Failed to create waiting transaction: {ex.Message}");
            }
        }

        public async Task<List<WaitingTransactionDto>> GetWaitingTransactionsAsync(int? staffId = null)
        {
            var transactions = new List<WaitingTransactionDto>();
            try
            {
                var sql = @"
                    SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
                           TableName, SeatIds, StaffId, Status, Notes, CreatedDate, ModifiedDate,
                           IsRefired, RefireReason, RefiredAt, RefiredByStaffId, RefiredByStaffName, RefiredItemIndices
                    FROM WaitingTransactions";

                if (staffId.HasValue)
                {
                    sql += " WHERE StaffId = @StaffId";
                }

                sql += " ORDER BY IsRefired DESC, CreatedDate ASC";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                var results = await connection.QueryAsync<dynamic>(sql, staffId.HasValue ? new { StaffId = staffId.Value } : null);

                foreach (var row in results)
                {
                    try
                    {
                        var itemsJson = (string)row.CartItems ?? "[]";
                        
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = null
                        };
                        
                        var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions) 
                            ?? new List<WaitingTransactionItemDto>();

                        if (_taxService != null && items.Count > 0)
                        {
                            await _taxService.UpdateOrderItemTaxesAsync(items);
                        }

                        var transaction = new WaitingTransactionDto
                        {
                            TransactionId = (string)row.TransactionId ?? "",
                            CustomerId = row.CustomerId != null ? (string)row.CustomerId : null,
                            TableId = row.TableId != null ? (string)row.TableId : null,
                            TableNumber = row.TableNumber != null ? (int?)row.TableNumber : null,
                            TableName = row.TableName != null ? (string)row.TableName : null,
                            SeatIds = row.SeatIds != null ? (string)row.SeatIds : null,
                            StaffId = row.StaffId != null ? (int?)row.StaffId : null,
                            Status = (string)row.Status ?? "Pending",
                            Notes = row.Notes != null ? (string)row.Notes : null,
                            CreatedDate = DateTime.Parse(row.CreatedDate),
                            ModifiedDate = DateTime.Parse(row.ModifiedDate),
                            Items = items,
                            // Refire properties
                            IsRefired = row.IsRefired != null && Convert.ToBoolean(row.IsRefired),
                            RefireReason = row.RefireReason != null ? (string)row.RefireReason : null,
                            RefiredAt = row.RefiredAt != null ? DateTime.Parse(row.RefiredAt) : null,
                            RefiredByStaffId = row.RefiredByStaffId != null ? (int?)row.RefiredByStaffId : null,
                            RefiredByStaffName = row.RefiredByStaffName != null ? (string)row.RefiredByStaffName : null,
                            RefiredItemIndices = row.RefiredItemIndices != null ? (string)row.RefiredItemIndices : null
                        };

                        if (!string.IsNullOrEmpty(transaction.TransactionId))
                        {
                            transactions.Add(transaction);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error parsing transaction row: {ex.Message}");
                    }
                }

                // Also merge active online orders from OnlineOrders table that might not be in WaitingTransactions
                try
                {
                    var existingTxIds = new HashSet<string>(transactions.Select(t => t.TransactionId), StringComparer.OrdinalIgnoreCase);
                    var onlineOrders = await GetOnlineOrdersAsync();
                    foreach (var o in onlineOrders)
                    {
                        if (o == null) continue;
                        string webTxId = $"web_{o.OrderNumber}";
                        if (!existingTxIds.Contains(o.OrderNumber) && !existingTxIds.Contains(o.CloudId) && !existingTxIds.Contains(webTxId))
                        {
                            // Only include active orders
                            if (o.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) || 
                                o.Status.Equals("DELIVERED", StringComparison.OrdinalIgnoreCase) || 
                                o.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string tableName = o.IsDelivery ? "Online Delivery" : "Online Pickup";
                            string notes = $"[ONLINE {(o.IsDelivery ? "DELIVERY" : "PICKUP")}] [{(o.IsPaid ? "PAID" : "UNPAID")}]";
                            if (!string.IsNullOrEmpty(o.CustomerName)) notes += $"\nCustomer: {o.CustomerName}";
                            if (!string.IsNullOrEmpty(o.CustomerPhone)) notes += $" ({o.CustomerPhone})";
                            if (!string.IsNullOrEmpty(o.DeliveryAddress)) notes += $"\nAddress: {o.DeliveryAddress}";
                            if (!string.IsNullOrEmpty(o.DeliveryLandmark)) notes += $"\nLandmark: {o.DeliveryLandmark}";
                            if (!string.IsNullOrEmpty(o.DeliveryNotes)) notes += $"\nInstructions: {o.DeliveryNotes}";

                            var onlineTx = new WaitingTransactionDto
                            {
                                TransactionId = webTxId,
                                CustomerId = o.CustomerPhone,
                                TableName = tableName,
                                Status = o.Status,
                                Notes = notes,
                                CreatedDate = DateTime.TryParse(o.UpdatedAt, out var dt) ? dt : DateTime.Now,
                                ModifiedDate = DateTime.Now,
                                Items = o.Items
                            };
                            transactions.Add(onlineTx);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Note on merging online orders into waiting: {ex.Message}");
                }

                Console.WriteLine($"✅ Retrieved {transactions.Count} waiting transactions");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting waiting transactions: {ex.Message}");
                throw new Exception($"Failed to get waiting transactions: {ex.Message}");
            }

            return transactions;
        }

        public async Task<WaitingTransactionDto?> GetWaitingTransactionByIdAsync(string transactionId)
        {
            var sql = @"
        SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
               TableName, StaffId, Status, Notes, SeatIds, CreatedDate, ModifiedDate
        FROM WaitingTransactions
        WHERE TransactionId = @TransactionId";

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var row = await connection.QueryFirstOrDefaultAsync<WaitingTransactionRaw>(
                sql,
                new { TransactionId = transactionId });

            if (row == null) return null;

            var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(
                row.CartItems ?? "[]",
                new JsonSerializerOptions { PropertyNamingPolicy = null }) ?? new();

            if (_taxService != null && items.Count > 0)
                await _taxService.UpdateOrderItemTaxesAsync(items);

            return new WaitingTransactionDto
            {
                TransactionId = row.TransactionId,
                CustomerId = row.CustomerId,
                TableId = row.TableId,
                TableNumber = row.TableNumber,
                TableName = row.TableName,
                StaffId = row.StaffId,
                Status = row.Status,
                Notes = row.Notes,
                SeatIds = row.SeatIds,
                CreatedDate = DateTime.Parse(row.CreatedDate),
                ModifiedDate = DateTime.Parse(row.ModifiedDate),
                Items = items
            };
        }

        public async Task<bool> UpdateWaitingTransactionStatusAsync(string transactionId, string status)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // If status is "Completed", also mark all items as completed
                if (status == "Completed")
                {
                    // Get current transaction with items
                    var transaction = await GetWaitingTransactionByIdAsync(transactionId);
                    if (transaction != null && transaction.Items != null && transaction.Items.Count > 0)
                    {
                        // Mark all items as completed
                        foreach (var item in transaction.Items)
                        {
                            item.IsCompleted = true;
                        }

                        // Serialize updated items
                        var jsonOptions = new JsonSerializerOptions
                        {
                            WriteIndented = false,
                            PropertyNamingPolicy = null
                        };
                        var updatedCartItemsJson = JsonSerializer.Serialize(transaction.Items, jsonOptions);

                        // Update both status and cart items
                        var sql = @"
                            UPDATE WaitingTransactions 
                            SET Status = @Status, CartItems = @CartItems, ModifiedDate = @ModifiedDate 
                            WHERE TransactionId = @TransactionId";

                        int result = await connection.ExecuteAsync(sql, new
                        {
                            Status = status,
                            CartItems = updatedCartItemsJson,
                            ModifiedDate = DateTime.Now,
                            TransactionId = transactionId
                        });

                        Console.WriteLine($"✅ Transaction status updated to 'Completed' and all items marked as completed (ID: {transactionId}). Rows affected: {result}");
                        return result > 0;
                    }
                }

                // For other statuses, just update the status
                var simpleSql = @"
                    UPDATE WaitingTransactions 
                    SET Status = @Status, ModifiedDate = @ModifiedDate 
                    WHERE TransactionId = @TransactionId";

                int simpleResult = await connection.ExecuteAsync(simpleSql, new
                {
                    Status = status,
                    ModifiedDate = DateTime.Now,
                    TransactionId = transactionId
                });

                Console.WriteLine($"✅ Transaction status updated to '{status}' (ID: {transactionId}). Rows affected: {simpleResult}");
                return simpleResult > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating transaction status: {ex.Message}");
                throw new Exception($"Failed to update transaction status: {ex.Message}");
            }
        }

        public async Task<bool> DeleteWaitingTransactionAsync(string transactionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Get table ID before deleting
                var tableId = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT TableId FROM WaitingTransactions WHERE TransactionId = @id",
                    new { id = transactionId },
                    transaction);

                // Delete related records FIRST to avoid foreign key constraint errors
                // 1. Delete print bill requests
                await connection.ExecuteAsync(
                    "DELETE FROM PrintBillRequests WHERE transactionId = @id",
                    new { id = transactionId },
                    transaction);

                // 2. Delete pay entire bill requests
                await connection.ExecuteAsync(
                    "DELETE FROM PayEntireBillRequests WHERE transactionId = @id",
                    new { id = transactionId },
                    transaction);

                // 3. Delete tax adjustment audit logs
                await connection.ExecuteAsync(
                    "DELETE FROM TaxAdjustmentAuditLog WHERE transactionId = @id",
                    new { id = transactionId },
                    transaction);

                // 4. Now delete the waiting transaction itself
                var deleted = await connection.ExecuteAsync(
                    "DELETE FROM WaitingTransactions WHERE TransactionId = @id",
                    new { id = transactionId },
                    transaction);

                // 5. Update table status if this was the last order on the table
                if (deleted > 0 && !string.IsNullOrEmpty(tableId))
                {
                    // Check remaining orders
                    var remaining = await connection.ExecuteScalarAsync<long>(
                        "SELECT COUNT(*) FROM WaitingTransactions WHERE TableId = @tableId",
                        new { tableId },
                        transaction);

                    if (remaining == 0)
                    {
                        // Free the table
                        await connection.ExecuteAsync(@"
                            UPDATE Tables 
                            SET status = 'Available', currentTransactionId = NULL, currentCustomerId = NULL
                            WHERE tableId = @tableId",
                            new { tableId }, transaction);

                        // Free all seats on the table
                        await connection.ExecuteAsync(@"
                            UPDATE Seats 
                            SET status = 'Available', customerId = NULL
                            WHERE tableId = @tableId",
                            new { tableId }, transaction);
                    }
                }

                transaction.Commit();
                return deleted > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<WaitingTransactionDto>> GetOrdersByTableAsync(string tableId)
            => await GetOrdersAsync("TableId", tableId);

        public async Task<List<WaitingTransactionDto>> GetOrdersByCustomerAsync(string customerId)
            => await GetOrdersAsync("CustomerId", customerId);

        private async Task<List<WaitingTransactionDto>> GetOrdersAsync(string columnName, string id)
        {
            var allowedColumns = new[] { "TableId", "CustomerId" };
            if (!allowedColumns.Contains(columnName)) throw new ArgumentException("Invalid column");

            var sql = $@"
        SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
               TableName, StaffId, Status, Notes, CreatedDate, ModifiedDate
        FROM WaitingTransactions
        WHERE {columnName} = @id
        ORDER BY CreatedDate DESC";

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var rows = await connection.QueryAsync<WaitingTransactionRaw>(sql, new { id });

            return rows.Select(row => new WaitingTransactionDto
            {
                TransactionId = row.TransactionId,
                CustomerId = row.CustomerId,
                TableId = row.TableId,
                TableNumber = row.TableNumber, 
                TableName = row.TableName,
                StaffId = row.StaffId,
                Status = row.Status,
                Notes = row.Notes,
                CreatedDate = DateTime.Parse(row.CreatedDate),
                ModifiedDate = DateTime.Parse(row.ModifiedDate),
                Items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(
                    row.CartItems ?? "[]",
                    new JsonSerializerOptions { PropertyNamingPolicy = null }) ?? new()
            }).ToList();
        }

        public async Task<bool> UpdateWaitingTransactionItemsAsync(string transactionId, List<WaitingTransactionItemDto> items, int? staffId = null)
        {
            try
            {
                // Recalculate taxes if tax service is available
                if (_taxService != null && items.Count > 0)
                {
                    await _taxService.UpdateOrderItemTaxesAsync(items);
                    Console.WriteLine($"✅ Taxes recalculated for {items.Count} items");
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = null
                };

                var cartItemsJson = JsonSerializer.Serialize(items, jsonOptions);

                var sql = @"
            UPDATE WaitingTransactions 
            SET CartItems = @CartItems,
                ModifiedDate = @ModifiedDate
                " + (staffId.HasValue ? ", StaffId = @StaffId" : "") + @"
            WHERE TransactionId = @TransactionId";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                int result = await connection.ExecuteAsync(sql, new
                {
                    CartItems = cartItemsJson,
                    ModifiedDate = DateTime.Now,
                    TransactionId = transactionId,
                    StaffId = staffId
                });

                Console.WriteLine($"✅ Transaction items updated (ID: {transactionId}). Rows affected: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating transaction items: {ex.Message}");
                throw new Exception($"Failed to update transaction items: {ex.Message}");
            }
        }

        public async Task<List<OnlineOrderDto>> GetOnlineOrdersAsync(string? status = null)
        {
            var orders = new List<OnlineOrderDto>();
            try
            {
                var sql = @"
                    SELECT cloud_id AS CloudId, order_number AS OrderNumber, status AS Status,
                           payment_method AS PaymentMethod, amount AS Amount, raw_json AS RawJson,
                           stock_applied_at AS StockAppliedAt, stock_reversed_at AS StockReversedAt,
                           updated_at AS UpdatedAt
                    FROM OnlineOrders";

                if (!string.IsNullOrEmpty(status))
                {
                    sql += " WHERE status = @Status";
                }

                sql += " ORDER BY updated_at DESC";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var results = await connection.QueryAsync<dynamic>(sql, !string.IsNullOrEmpty(status) ? new { Status = status } : null);

                foreach (var row in results)
                {
                    try
                    {
                        var dto = ParseOnlineOrderRow(row);
                        if (dto != null)
                        {
                            orders.Add(dto);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error parsing online order row: {ex.Message}");
                    }
                }

                Console.WriteLine($"✅ Retrieved {orders.Count} online orders from OnlineOrders table");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting online orders: {ex.Message}");
            }
            return orders;
        }

        public async Task<OnlineOrderDto?> GetOnlineOrderByIdAsync(string idOrOrderNumber)
        {
            try
            {
                var sql = @"
                    SELECT cloud_id AS CloudId, order_number AS OrderNumber, status AS Status,
                           payment_method AS PaymentMethod, amount AS Amount, raw_json AS RawJson,
                           stock_applied_at AS StockAppliedAt, stock_reversed_at AS StockReversedAt,
                           updated_at AS UpdatedAt
                    FROM OnlineOrders
                    WHERE cloud_id = @Id OR order_number = @Id
                    LIMIT 1";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var row = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Id = idOrOrderNumber });
                if (row != null)
                {
                    return ParseOnlineOrderRow(row);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting online order by ID: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> UpdateOnlineOrderStatusAsync(string idOrOrderNumber, string status, string? paymentMethod = null, string? note = null)
        {
            try
            {
                var sql = @"
                    UPDATE OnlineOrders 
                    SET status = @Status, 
                        updated_at = @UpdatedAt
                        " + (!string.IsNullOrEmpty(paymentMethod) ? ", payment_method = @PaymentMethod" : "") + @"
                    WHERE cloud_id = @Id OR order_number = @Id";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                int result = await connection.ExecuteAsync(sql, new
                {
                    Status = status,
                    UpdatedAt = DateTime.UtcNow.ToString("o"),
                    PaymentMethod = paymentMethod,
                    Id = idOrOrderNumber
                });

                // Also sync status on matching waiting transaction if present
                try
                {
                    var updateWaitingSql = @"
                        UPDATE WaitingTransactions
                        SET Status = @Status, ModifiedDate = @ModifiedDate
                        WHERE TransactionId = @TxId OR TransactionId = @WebTxId";

                    await connection.ExecuteAsync(updateWaitingSql, new
                    {
                        Status = status,
                        ModifiedDate = DateTime.Now.ToString("o"),
                        TxId = idOrOrderNumber,
                        WebTxId = $"web_{idOrOrderNumber}"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Note on waiting transaction status sync: {ex.Message}");
                }

                Console.WriteLine($"✅ Online order {idOrOrderNumber} status updated to {status}");
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating online order status: {ex.Message}");
                return false;
            }
        }

        private OnlineOrderDto ParseOnlineOrderRow(dynamic row)
        {
            var dto = new OnlineOrderDto
            {
                CloudId = (string)row.CloudId ?? "",
                OrderNumber = (string)row.OrderNumber ?? "",
                Status = (string)row.Status ?? "PENDING",
                PaymentMethod = row.PaymentMethod != null ? (string)row.PaymentMethod : null,
                Amount = row.Amount != null ? Convert.ToDecimal(row.Amount) : null,
                RawJson = (string)row.RawJson ?? "{}",
                StockAppliedAt = row.StockAppliedAt != null ? (string)row.StockAppliedAt : null,
                StockReversedAt = row.StockReversedAt != null ? (string)row.StockReversedAt : null,
                UpdatedAt = (string)row.UpdatedAt ?? ""
            };

            dto.IsPaid = dto.Status.Equals("PAID", StringComparison.OrdinalIgnoreCase) ||
                         (dto.PaymentMethod != null && dto.PaymentMethod.IndexOf("SUCCESS", StringComparison.OrdinalIgnoreCase) >= 0);

            // Parse RawJson for enriched details (customer, address, items)
            try
            {
                using var doc = JsonDocument.Parse(dto.RawJson);
                var root = doc.RootElement;

                // 1. Customer info (Flat + Nested objects)
                string customerId = null;
                if (root.TryGetProperty("customer_id", out var cid) && cid.ValueKind == JsonValueKind.String) customerId = cid.GetString();
                if (string.IsNullOrEmpty(customerId) && root.TryGetProperty("customerId", out var cid2) && cid2.ValueKind == JsonValueKind.String) customerId = cid2.GetString();
                if (string.IsNullOrEmpty(customerId) && root.TryGetProperty("user_id", out var uid) && uid.ValueKind == JsonValueKind.String) customerId = uid.GetString();
                if (string.IsNullOrEmpty(customerId) && root.TryGetProperty("userId", out var uid2) && uid2.ValueKind == JsonValueKind.String) customerId = uid2.GetString();
                if (string.IsNullOrEmpty(customerId) && root.TryGetProperty("client_id", out var clid) && clid.ValueKind == JsonValueKind.String) customerId = clid.GetString();

                string[] nameKeys = { "customer_name", "customerName", "name", "full_name", "fullName", "client_name", "recipient_name", "receiver_name", "contact_name" };
                foreach (var k in nameKeys)
                {
                    if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) && !v.GetString().Equals("Online Customer", StringComparison.OrdinalIgnoreCase))
                    {
                        dto.CustomerName = v.GetString().Trim();
                        break;
                    }
                }

                string[] phoneKeys = { "customer_phone", "customerPhone", "phone", "phone_number", "phoneNumber", "tel", "mobile", "recipient_phone", "receiver_phone", "contact_phone" };
                foreach (var k in phoneKeys)
                {
                    if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                    {
                        dto.CustomerPhone = v.GetString().Trim();
                        break;
                    }
                }

                string[] objKeys = { "customer", "user", "client", "buyer", "contact", "recipient", "shipping_address", "delivery_info", "delivery_address", "metadata" };
                foreach (var ok in objKeys)
                {
                    if (root.TryGetProperty(ok, out var objElem) && objElem.ValueKind == JsonValueKind.Object)
                    {
                        if (string.IsNullOrEmpty(customerId) && objElem.TryGetProperty("id", out var idElem) && idElem.ValueKind == JsonValueKind.String) customerId = idElem.GetString();
                        if (string.IsNullOrEmpty(customerId) && objElem.TryGetProperty("customer_id", out var idElem2) && idElem2.ValueKind == JsonValueKind.String) customerId = idElem2.GetString();

                        if (string.IsNullOrEmpty(dto.CustomerName) && objElem.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String && !n.GetString().Equals("Online Customer", StringComparison.OrdinalIgnoreCase)) dto.CustomerName = n.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerName) && objElem.TryGetProperty("full_name", out var fn) && fn.ValueKind == JsonValueKind.String && !fn.GetString().Equals("Online Customer", StringComparison.OrdinalIgnoreCase)) dto.CustomerName = fn.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerName) && objElem.TryGetProperty("recipient_name", out var rn) && rn.ValueKind == JsonValueKind.String) dto.CustomerName = rn.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerName) && objElem.TryGetProperty("customer_name", out var cn2) && cn2.ValueKind == JsonValueKind.String) dto.CustomerName = cn2.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerName))
                        {
                            string f = objElem.TryGetProperty("first_name", out var fnm) && fnm.ValueKind == JsonValueKind.String ? fnm.GetString() : "";
                            string l = objElem.TryGetProperty("last_name", out var lnm) && lnm.ValueKind == JsonValueKind.String ? lnm.GetString() : "";
                            var full = $"{f} {l}".Trim();
                            if (!string.IsNullOrEmpty(full)) dto.CustomerName = full;
                        }

                        if (string.IsNullOrEmpty(dto.CustomerPhone) && objElem.TryGetProperty("phone_number", out var p) && p.ValueKind == JsonValueKind.String) dto.CustomerPhone = p.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerPhone) && objElem.TryGetProperty("phone", out var p2) && p2.ValueKind == JsonValueKind.String) dto.CustomerPhone = p2.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerPhone) && objElem.TryGetProperty("mobile", out var p3) && p3.ValueKind == JsonValueKind.String) dto.CustomerPhone = p3.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerPhone) && objElem.TryGetProperty("recipient_phone", out var rp) && rp.ValueKind == JsonValueKind.String) dto.CustomerPhone = rp.GetString();
                        if (string.IsNullOrEmpty(dto.CustomerPhone) && objElem.TryGetProperty("customer_phone", out var cp2) && cp2.ValueKind == JsonValueKind.String) dto.CustomerPhone = cp2.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(dto.CustomerName) && !string.IsNullOrWhiteSpace(dto.CustomerPhone))
                {
                    dto.CustomerName = $"Customer ({dto.CustomerPhone})";
                }

                // 2. Delivery address info
                if (root.TryGetProperty("delivery_address", out var addrElem))
                {
                    if (addrElem.ValueKind == JsonValueKind.Object)
                    {
                        var parts = new List<string>();
                        if (addrElem.TryGetProperty("quarter", out var q) && q.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(q.GetString())) parts.Add(q.GetString());
                        if (addrElem.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(a.GetString())) parts.Add(a.GetString());
                        if (addrElem.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(c.GetString())) parts.Add(c.GetString());
                        if (parts.Count > 0) dto.DeliveryAddress = string.Join(", ", parts);

                        if (addrElem.TryGetProperty("quarter", out var qv) && qv.ValueKind == JsonValueKind.String) dto.DeliveryQuarter = qv.GetString();
                        if (addrElem.TryGetProperty("landmark", out var l) && l.ValueKind == JsonValueKind.String) dto.DeliveryLandmark = l.GetString();
                        if (addrElem.TryGetProperty("directions", out var d) && d.ValueKind == JsonValueKind.String) dto.DeliveryNotes = d.GetString();
                        if (addrElem.TryGetProperty("recipient_phone", out var rp) && string.IsNullOrEmpty(dto.CustomerPhone))
                            dto.CustomerPhone = rp.GetString();
                    }
                    else if (addrElem.ValueKind == JsonValueKind.String)
                    {
                        dto.DeliveryAddress = addrElem.GetString();
                    }
                }

                if (string.IsNullOrEmpty(dto.DeliveryAddress))
                {
                    if (root.TryGetProperty("address", out var aFlat) && aFlat.ValueKind == JsonValueKind.String)
                        dto.DeliveryAddress = aFlat.GetString();
                }

                // 3. Channel checks from source or delivery_type
                string dtStr = "";
                if (root.TryGetProperty("delivery_type", out var dtElem) && dtElem.ValueKind == JsonValueKind.String)
                    dtStr = dtElem.GetString() ?? "";
                else if (root.TryGetProperty("fulfillment_type", out var ftElem) && ftElem.ValueKind == JsonValueKind.String)
                    dtStr = ftElem.GetString() ?? "";
                else if (root.TryGetProperty("delivery_method", out var dmElem) && dmElem.ValueKind == JsonValueKind.String)
                    dtStr = dmElem.GetString() ?? "";
                else if (root.TryGetProperty("type", out var tElem) && tElem.ValueKind == JsonValueKind.String)
                    dtStr = tElem.GetString() ?? "";

                if (dtStr.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dtStr.IndexOf("takeaway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dtStr.IndexOf("dine_in", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dtStr.IndexOf("in_store", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    dto.IsDelivery = false;
                }
                else if (root.TryGetProperty("is_pickup", out var ipElem) && (ipElem.ValueKind == JsonValueKind.True || (ipElem.ValueKind == JsonValueKind.String && ipElem.GetString().Equals("true", StringComparison.OrdinalIgnoreCase))))
                {
                    dto.IsDelivery = false;
                }
                else
                {
                    dto.IsDelivery = true;
                }

                // 4. Items list
                if (root.TryGetProperty("items", out var itemsElem) && itemsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var itemElem in itemsElem.EnumerateArray())
                    {
                        var name = itemElem.TryGetProperty("name", out var iname) && iname.ValueKind == JsonValueKind.String ? iname.GetString() :
                                   (itemElem.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String ? desc.GetString() : "Item");
                        var qty = itemElem.TryGetProperty("quantity", out var q) ? (q.ValueKind == JsonValueKind.Number ? q.GetInt32() : 1) : 1;
                        var price = itemElem.TryGetProperty("unit_price", out var up) && up.ValueKind == JsonValueKind.Number ? up.GetDecimal() :
                                    (itemElem.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetDecimal() : 
                                    (itemElem.TryGetProperty("unit", out var u) && u.TryGetProperty("price", out var uup) && uup.ValueKind == JsonValueKind.Number ? uup.GetDecimal() : 0m));
                        var total = itemElem.TryGetProperty("total_price", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetDecimal() :
                                    (itemElem.TryGetProperty("total_amount", out var ta) && ta.ValueKind == JsonValueKind.Number ? ta.GetDecimal() : price * qty);

                        dto.Items.Add(new WaitingTransactionItemDto
                        {
                            ProductId = itemElem.TryGetProperty("product_id", out var pid) && pid.ValueKind == JsonValueKind.String ? pid.GetString() ?? "" : "",
                            Name = name ?? "Item",
                            Quantity = qty,
                            Price = price,
                            Total = total
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing raw_json: {ex.Message}");
            }

            return dto;
        }

        private class WaitingTransactionRaw
        {
            public string TransactionId { get; set; } = "";
            public string CartItems { get; set; } = "";
            public string? CustomerId { get; set; }
            public string? TableId { get; set; }
            public int? TableNumber { get; set; }
            public string? TableName { get; set; }
            public int? StaffId { get; set; }
            public string Status { get; set; } = "";
            public string? Notes { get; set; }
            public string? SeatIds { get; set; }
            public string CreatedDate { get; set; } = ""; 
            public string ModifiedDate { get; set; } = "";
        }
    }
}
