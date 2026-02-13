using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Text.Json;

namespace PottaAPI.Services
{
    // Order/transaction management service
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
                // Calculate taxes for all items if tax service is available
                if (_taxService != null)
                {
                    await _taxService.UpdateOrderItemTaxesAsync(transaction.Items);
                    Console.WriteLine($"✅ Taxes calculated for {transaction.Items.Count} items");
                }

                var transactionId = "M" + DateTime.Now.ToString("yyyyMMddHHmmss");

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
                           TableName, StaffId, Status, CreatedDate, ModifiedDate
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

                        // Calculate taxes for items if tax service is available
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
                           TableName, StaffId, Status, CreatedDate, ModifiedDate
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

                    // Calculate taxes for items if tax service is available
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
                var sql = "DELETE FROM WaitingTransactions WHERE TransactionId = @TransactionId";
                var parameters = new SqliteParameter[]
                {
                    new SqliteParameter("@TransactionId", transactionId)
                };

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = 30;
                command.Parameters.AddRange(parameters);

                int result = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"✅ Transaction deleted (ID: {transactionId}). Rows affected: {result}");
                return result > 0;
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
