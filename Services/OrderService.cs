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
                    // This means the mobile only needs to call POST /api/orders —
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
