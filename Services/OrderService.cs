using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Text.Json;

namespace PottaAPI.Services
{
    public class OrderService : IOrderService
    {
        private readonly string _connectionString;
        private readonly ITaxService? _taxService;

        public OrderService(string connectionString, ITaxService? taxService = null)
        {
            _connectionString = connectionString;
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
                        TableName, StaffId, CreatedDate, ModifiedDate, Status
                    ) 
                    VALUES (
                        @TransactionId, @CartItems, @CustomerId, @TableId, @TableNumber, 
                        @TableName, @StaffId, @CreatedDate, @ModifiedDate, @Status
                    )";

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = null
                };
                
                var cartItemsJson = JsonSerializer.Serialize(transaction.Items, jsonOptions);

                var parameters = new SqliteParameter[]
                {
                    new SqliteParameter("@TransactionId", transactionId),
                    new SqliteParameter("@CartItems", cartItemsJson),
                    new SqliteParameter("@CustomerId", transaction.CustomerId ?? (object)DBNull.Value),
                    new SqliteParameter("@TableId", transaction.TableId ?? (object)DBNull.Value),
                    new SqliteParameter("@TableNumber", transaction.TableNumber ?? (object)DBNull.Value),
                    new SqliteParameter("@TableName", transaction.TableName ?? (object)DBNull.Value),
                    new SqliteParameter("@StaffId", transaction.StaffId),
                    new SqliteParameter("@CreatedDate", DateTime.Now),
                    new SqliteParameter("@ModifiedDate", DateTime.Now),
                    new SqliteParameter("@Status", "Pending")
                };

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;
                command.Parameters.AddRange(parameters);
                
                int result = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"✅ Waiting transaction created (ID: {transactionId}). Rows affected: {result}");
                return transactionId;
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
                           TableName, StaffId, Status, Notes, CreatedDate, ModifiedDate
                    FROM WaitingTransactions";

                if (staffId.HasValue)
                {
                    sql += " WHERE StaffId = @StaffId";
                }

                sql += " ORDER BY CreatedDate DESC";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;

                if (staffId.HasValue)
                {
                    command.Parameters.AddWithValue("@StaffId", staffId.Value);
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                        
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
                            TransactionId = reader["TransactionId"]?.ToString() ?? "",
                            CustomerId = reader["CustomerId"] != DBNull.Value 
                                ? reader["CustomerId"]?.ToString() 
                                : null,
                            TableId = reader["TableId"] != DBNull.Value 
                                ? reader["TableId"]?.ToString() 
                                : null,
                            TableNumber = reader["TableNumber"] != DBNull.Value 
                                ? Convert.ToInt32(reader["TableNumber"]) 
                                : null,
                            TableName = reader["TableName"] != DBNull.Value 
                                ? reader["TableName"]?.ToString() 
                                : null,
                            StaffId = reader["StaffId"] != DBNull.Value 
                                ? Convert.ToInt32(reader["StaffId"]) 
                                : null,
                            Status = reader["Status"]?.ToString() ?? "Pending",
                            Notes = reader["Notes"] != DBNull.Value 
                                ? reader["Notes"]?.ToString() 
                                : null,
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                            ModifiedDate = Convert.ToDateTime(reader["ModifiedDate"]),
                            Items = items
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
            try
            {
                var sql = @"
                    SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
                           TableName, StaffId, Status, Notes, CreatedDate, ModifiedDate
                    FROM WaitingTransactions
                    WHERE TransactionId = @TransactionId";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;
                command.Parameters.AddWithValue("@TransactionId", transactionId);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                    
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                    var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions) 
                        ?? new List<WaitingTransactionItemDto>();

                    if (_taxService != null && items.Count > 0)
                    {
                        await _taxService.UpdateOrderItemTaxesAsync(items);
                    }

                    return new WaitingTransactionDto
                    {
                        TransactionId = reader["TransactionId"]?.ToString() ?? "",
                        CustomerId = reader["CustomerId"] != DBNull.Value 
                            ? reader["CustomerId"]?.ToString() 
                            : null,
                        TableId = reader["TableId"] != DBNull.Value 
                            ? reader["TableId"]?.ToString() 
                            : null,
                        TableNumber = reader["TableNumber"] != DBNull.Value 
                            ? Convert.ToInt32(reader["TableNumber"]) 
                            : null,
                        TableName = reader["TableName"] != DBNull.Value 
                            ? reader["TableName"]?.ToString() 
                            : null,
                        StaffId = reader["StaffId"] != DBNull.Value 
                            ? Convert.ToInt32(reader["StaffId"]) 
                            : null,
                        Status = reader["Status"]?.ToString() ?? "Pending",
                        Notes = reader["Notes"] != DBNull.Value 
                            ? reader["Notes"]?.ToString() 
                            : null,
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        ModifiedDate = Convert.ToDateTime(reader["ModifiedDate"]),
                        Items = items
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting transaction by ID: {ex.Message}");
                throw new Exception($"Failed to get transaction: {ex.Message}");
            }
        }


        public async Task<bool> UpdateWaitingTransactionStatusAsync(string transactionId, string status)
        {
            try
            {
                var sql = @"
                    UPDATE WaitingTransactions 
                    SET Status = @Status, ModifiedDate = @ModifiedDate 
                    WHERE TransactionId = @TransactionId";

                var parameters = new SqliteParameter[]
                {
                    new SqliteParameter("@Status", status),
                    new SqliteParameter("@ModifiedDate", DateTime.Now),
                    new SqliteParameter("@TransactionId", transactionId)
                };

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;
                command.Parameters.AddRange(parameters);

                int result = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"✅ Transaction status updated (ID: {transactionId}). Rows affected: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating transaction status: {ex.Message}");
                throw new Exception($"Failed to update transaction status: {ex.Message}");
            }
        }

        public async Task<bool> DeleteWaitingTransactionAsync(string transactionId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Get the table ID before deleting
                    var getTableSql = "SELECT TableId FROM WaitingTransactions WHERE TransactionId = @TransactionId";
                    string? tableId = null;
                    
                    using (var getCommand = connection.CreateCommand())
                    {
                        getCommand.CommandText = getTableSql;
                        getCommand.Parameters.AddWithValue("@TransactionId", transactionId);
                        var result = await getCommand.ExecuteScalarAsync();
                        tableId = result?.ToString();
                    }

                    // Delete the transaction
                    var deleteSql = "DELETE FROM WaitingTransactions WHERE TransactionId = @TransactionId";
                    int deleteResult;
                    
                    using (var deleteCommand = connection.CreateCommand())
                    {
                        deleteCommand.CommandText = deleteSql;
                        deleteCommand.Parameters.AddWithValue("@TransactionId", transactionId);
                        deleteResult = await deleteCommand.ExecuteNonQueryAsync();
                    }

                    if (deleteResult > 0 && !string.IsNullOrEmpty(tableId))
                    {
                        // Check if there are any remaining orders for this table
                        var checkOrdersSql = "SELECT COUNT(*) FROM WaitingTransactions WHERE TableId = @TableId";
                        long remainingOrders = 0;
                        
                        using (var checkCommand = connection.CreateCommand())
                        {
                            checkCommand.CommandText = checkOrdersSql;
                            checkCommand.Parameters.AddWithValue("@TableId", tableId);
                            var countResult = await checkCommand.ExecuteScalarAsync();
                            remainingOrders = countResult != null ? Convert.ToInt64(countResult) : 0;
                        }

                        // If no more orders, clear table and seats
                        if (remainingOrders == 0)
                        {
                            // Update table status to Available and clear transaction/customer
                            var updateTableSql = @"
                                UPDATE Tables 
                                SET status = 'Available', 
                                    currentTransactionId = NULL, 
                                    currentCustomerId = NULL,
                                    modifiedDate = CURRENT_TIMESTAMP
                                WHERE tableId = @TableId";
                            
                            using (var updateTableCommand = connection.CreateCommand())
                            {
                                updateTableCommand.CommandText = updateTableSql;
                                updateTableCommand.Parameters.AddWithValue("@TableId", tableId);
                                await updateTableCommand.ExecuteNonQueryAsync();
                            }

                            // Update all seats to Available and clear customer
                            var updateSeatsSql = @"
                                UPDATE Seats 
                                SET status = 'Available', 
                                    customerId = NULL,
                                    modifiedDate = CURRENT_TIMESTAMP
                                WHERE tableId = @TableId";
                            
                            using (var updateSeatsCommand = connection.CreateCommand())
                            {
                                updateSeatsCommand.CommandText = updateSeatsSql;
                                updateSeatsCommand.Parameters.AddWithValue("@TableId", tableId);
                                await updateSeatsCommand.ExecuteNonQueryAsync();
                            }

                            Console.WriteLine($"✅ Transaction deleted and table {tableId} status reset to Available");
                        }
                        else
                        {
                            Console.WriteLine($"✅ Transaction deleted but table {tableId} still has {remainingOrders} remaining orders");
                        }
                    }

                    transaction.Commit();
                    return deleteResult > 0;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting transaction: {ex.Message}");
                throw new Exception($"Failed to delete transaction: {ex.Message}");
            }
        }


        public async Task<List<WaitingTransactionDto>> GetOrdersByTableAsync(string tableId)
        {
            var transactions = new List<WaitingTransactionDto>();
            try
            {
                var sql = @"
                    SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
                           TableName, StaffId, Status, CreatedDate, ModifiedDate
                    FROM WaitingTransactions
                    WHERE TableId = @TableId
                    ORDER BY CreatedDate DESC";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;
                command.Parameters.AddWithValue("@TableId", tableId);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                    
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                    var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions) 
                        ?? new List<WaitingTransactionItemDto>();

                    transactions.Add(new WaitingTransactionDto
                    {
                        TransactionId = reader["TransactionId"]?.ToString() ?? "",
                        CustomerId = reader["CustomerId"] != DBNull.Value 
                            ? reader["CustomerId"]?.ToString() 
                            : null,
                        TableId = reader["TableId"]?.ToString(),
                        TableNumber = reader["TableNumber"] != DBNull.Value 
                            ? Convert.ToInt32(reader["TableNumber"]) 
                            : null,
                        TableName = reader["TableName"]?.ToString(),
                        StaffId = reader["StaffId"] != DBNull.Value 
                            ? Convert.ToInt32(reader["StaffId"]) 
                            : null,
                        Status = reader["Status"]?.ToString() ?? "Pending",
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        ModifiedDate = Convert.ToDateTime(reader["ModifiedDate"]),
                        Items = items
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting orders by table: {ex.Message}");
            }

            return transactions;
        }

        public async Task<List<WaitingTransactionDto>> GetOrdersByCustomerAsync(string customerId)
        {
            var transactions = new List<WaitingTransactionDto>();
            try
            {
                var sql = @"
                    SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
                           TableName, StaffId, Status, CreatedDate, ModifiedDate
                    FROM WaitingTransactions
                    WHERE CustomerId = @CustomerId
                    ORDER BY CreatedDate DESC";

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;
                command.Parameters.AddWithValue("@CustomerId", customerId);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                    
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                    var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions) 
                        ?? new List<WaitingTransactionItemDto>();

                    transactions.Add(new WaitingTransactionDto
                    {
                        TransactionId = reader["TransactionId"]?.ToString() ?? "",
                        CustomerId = reader["CustomerId"]?.ToString(),
                        TableId = reader["TableId"] != DBNull.Value 
                            ? reader["TableId"]?.ToString() 
                            : null,
                        TableNumber = reader["TableNumber"] != DBNull.Value 
                            ? Convert.ToInt32(reader["TableNumber"]) 
                            : null,
                        TableName = reader["TableName"] != DBNull.Value 
                            ? reader["TableName"]?.ToString() 
                            : null,
                        StaffId = reader["StaffId"] != DBNull.Value 
                            ? Convert.ToInt32(reader["StaffId"]) 
                            : null,
                        Status = reader["Status"]?.ToString() ?? "Pending",
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        ModifiedDate = Convert.ToDateTime(reader["ModifiedDate"]),
                        Items = items
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting orders by customer: {ex.Message}");
            }

            return transactions;
        }
    }
}
