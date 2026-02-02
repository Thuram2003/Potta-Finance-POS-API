using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Text.Json;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for managing orders/waiting transactions
    /// </summary>
    public class OrderService : IOrderService
    {
        private readonly string _connectionString;

        public OrderService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<string> CreateWaitingTransactionAsync(CreateWaitingTransactionDto transaction)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Generate transaction ID like the POS system does (M prefix for mobile)
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

                    // Serialize cart items with all fields (matches WPF app exactly)
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        PropertyNamingPolicy = null // Use exact property names
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
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);
                    
                    int result = command.ExecuteNonQuery();
                    Console.WriteLine($"✅ Waiting transaction created (ID: {transactionId}). Rows affected: {result}");
                    return transactionId;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error creating waiting transaction: {ex.Message}");
                    throw new Exception($"Failed to create waiting transaction: {ex.Message}");
                }
            });
        }

        public async Task<List<WaitingTransactionDto>> GetWaitingTransactionsAsync(int? staffId = null)
        {
            return await Task.Run(() =>
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
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;

                    if (staffId.HasValue)
                    {
                        command.Parameters.AddWithValue("@StaffId", staffId.Value);
                    }

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        try
                        {
                            var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                            
                            // Deserialize with exact property names (matches WPF app)
                            var jsonOptions = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = null // Use exact property names
                            };
                            
                            var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions) 
                                ?? new List<WaitingTransactionItemDto>();

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
            });
        }

        public async Task<WaitingTransactionDto?> GetWaitingTransactionByIdAsync(string transactionId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sql = @"
                        SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, 
                               TableName, StaffId, Status, CreatedDate, ModifiedDate
                        FROM WaitingTransactions
                        WHERE TransactionId = @TransactionId";

                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@TransactionId", transactionId);

                    using var reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                        
                        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                        var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions) 
                            ?? new List<WaitingTransactionItemDto>();

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
            });
        }



        public async Task<bool> UpdateWaitingTransactionStatusAsync(string transactionId, string status)
        {
            return await Task.Run(() =>
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
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    int result = command.ExecuteNonQuery();
                    Console.WriteLine($"✅ Transaction status updated (ID: {transactionId}). Rows affected: {result}");
                    return result > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error updating transaction status: {ex.Message}");
                    throw new Exception($"Failed to update transaction status: {ex.Message}");
                }
            });
        }

        public async Task<bool> DeleteWaitingTransactionAsync(string transactionId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sql = "DELETE FROM WaitingTransactions WHERE TransactionId = @TransactionId";
                    var parameters = new SqliteParameter[]
                    {
                        new SqliteParameter("@TransactionId", transactionId)
                    };

                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    int result = command.ExecuteNonQuery();
                    Console.WriteLine($"✅ Transaction deleted (ID: {transactionId}). Rows affected: {result}");
                    return result > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error deleting transaction: {ex.Message}");
                    throw new Exception($"Failed to delete transaction: {ex.Message}");
                }
            });
        }



        public async Task<List<WaitingTransactionDto>> GetOrdersByTableAsync(string tableId)
        {
            return await Task.Run(() =>
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
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@TableId", tableId);

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
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
            });
        }

        public async Task<List<WaitingTransactionDto>> GetOrdersByCustomerAsync(string customerId)
        {
            return await Task.Run(() =>
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
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@CustomerId", customerId);

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
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
            });
        }
    }
}
