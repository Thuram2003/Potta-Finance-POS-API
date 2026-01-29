using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Text.Json;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for managing orders/waiting transactions
    /// Implements the same logic as the WPF app's WaitingTransaction functionality
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
                        
                        // Deserialize with exact property names (matches WPF app)
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = null
                        };
                        
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

        public async Task<List<WaitingTransactionDto>> GetPendingOrdersAsync()
        {
            var allTransactions = await GetWaitingTransactionsAsync();
            return allTransactions.Where(t => t.Status == "Pending").ToList();
        }

        public async Task<OrderStatisticsDto> GetOrderStatisticsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT 
                            COUNT(*) as TotalOrders,
                            COUNT(CASE WHEN Status = 'Pending' THEN 1 END) as PendingOrders,
                            COUNT(CASE WHEN Status = 'Completed' THEN 1 END) as CompletedOrders,
                            MIN(CreatedDate) as OldestPending,
                            MAX(CreatedDate) as NewestOrder,
                            COUNT(CASE WHEN DATE(CreatedDate) = DATE('now') THEN 1 END) as OrdersToday
                        FROM WaitingTransactions";

                    using var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        var stats = new OrderStatisticsDto
                        {
                            TotalOrders = Convert.ToInt32(reader["TotalOrders"] ?? 0),
                            PendingOrders = Convert.ToInt32(reader["PendingOrders"] ?? 0),
                            CompletedOrders = Convert.ToInt32(reader["CompletedOrders"] ?? 0),
                            OldestPendingOrder = reader["OldestPending"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["OldestPending"]) 
                                : null,
                            NewestOrder = reader["NewestOrder"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["NewestOrder"]) 
                                : null,
                            OrdersToday = Convert.ToInt32(reader["OrdersToday"] ?? 0)
                        };

                        // Calculate total order values from actual cart items
                        reader.Close();
                        
                        // Get all transactions to calculate totals
                        var allTransactionsCommand = connection.CreateCommand();
                        allTransactionsCommand.CommandText = "SELECT CartItems, CreatedDate FROM WaitingTransactions";
                        
                        using var allReader = allTransactionsCommand.ExecuteReader();
                        decimal totalValue = 0;
                        decimal todayValue = 0;
                        var today = DateTime.Today;
                        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                        
                        while (allReader.Read())
                        {
                            var itemsJson = allReader["CartItems"]?.ToString() ?? "[]";
                            var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson, jsonOptions);
                            
                            if (items != null)
                            {
                                decimal orderTotal = CalculateOrderTotal(items);
                                totalValue += orderTotal;
                                
                                var createdDate = Convert.ToDateTime(allReader["CreatedDate"]);
                                if (createdDate.Date == today)
                                {
                                    todayValue += orderTotal;
                                }
                            }
                        }
                        
                        stats.TotalOrderValue = totalValue;
                        stats.OrderValueToday = todayValue;
                        
                        return stats;
                    }

                    return new OrderStatisticsDto();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error getting order statistics: {ex.Message}");
                    throw new Exception($"Failed to get order statistics: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Calculate total for an order from cart items (matches WPF app logic)
        /// </summary>
        private decimal CalculateOrderTotal(List<WaitingTransactionItemDto> items)
        {
            decimal total = 0;
            foreach (var item in items)
            {
                // Base subtotal: (Price × Quantity) - Discount
                decimal itemSubTotal = (item.Price * item.Quantity) - item.Discount;
                
                // Add modifier costs
                decimal modifierTotal = item.AppliedModifiers?.Sum(m => m.PriceChange) ?? 0;
                
                total += itemSubTotal + modifierTotal;
            }
            return total;
        }

        public async Task<List<StaffOrderSummaryDto>> GetStaffOrderSummaryAsync()
        {
            return await Task.Run(() =>
            {
                var summaries = new List<StaffOrderSummaryDto>();
                try
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT 
                            wt.StaffId,
                            s.FirstName || ' ' || COALESCE(s.LastName, '') as StaffName,
                            COUNT(*) as TotalOrders,
                            COUNT(CASE WHEN wt.Status = 'Pending' THEN 1 END) as PendingOrders,
                            COUNT(CASE WHEN wt.Status = 'Completed' THEN 1 END) as CompletedOrders,
                            MAX(wt.CreatedDate) as LastOrderDate,
                            GROUP_CONCAT(wt.CartItems, '|||') as AllCartItems
                        FROM WaitingTransactions wt
                        LEFT JOIN Staff s ON wt.StaffId = s.Id
                        WHERE wt.StaffId IS NOT NULL
                        GROUP BY wt.StaffId, StaffName
                        ORDER BY TotalOrders DESC";

                    using var reader = command.ExecuteReader();
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                    
                    while (reader.Read())
                    {
                        // Calculate total order value from cart items
                        decimal totalOrderValue = 0;
                        var allCartItemsStr = reader["AllCartItems"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(allCartItemsStr))
                        {
                            var cartItemsArray = allCartItemsStr.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var cartItemsJson in cartItemsArray)
                            {
                                var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(cartItemsJson, jsonOptions);
                                if (items != null)
                                {
                                    totalOrderValue += CalculateOrderTotal(items);
                                }
                            }
                        }
                        
                        summaries.Add(new StaffOrderSummaryDto
                        {
                            StaffId = Convert.ToInt32(reader["StaffId"] ?? 0),
                            StaffName = reader["StaffName"]?.ToString() ?? "Unknown",
                            TotalOrders = Convert.ToInt32(reader["TotalOrders"] ?? 0),
                            PendingOrders = Convert.ToInt32(reader["PendingOrders"] ?? 0),
                            CompletedOrders = Convert.ToInt32(reader["CompletedOrders"] ?? 0),
                            LastOrderDate = reader["LastOrderDate"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["LastOrderDate"]) 
                                : null,
                            TotalOrderValue = totalOrderValue
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error getting staff order summary: {ex.Message}");
                }

                return summaries;
            });
        }

        public async Task<List<TableOrderSummaryDto>> GetTableOrderSummaryAsync()
        {
            return await Task.Run(() =>
            {
                var summaries = new List<TableOrderSummaryDto>();
                try
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT 
                            wt.TableId,
                            wt.TableName,
                            wt.TableNumber,
                            COUNT(*) as ActiveOrders,
                            MIN(wt.CreatedDate) as FirstOrderTime,
                            MAX(wt.CreatedDate) as LastOrderTime,
                            t.status as TableStatus,
                            GROUP_CONCAT(wt.CartItems, '|||') as AllCartItems
                        FROM WaitingTransactions wt
                        LEFT JOIN Tables t ON wt.TableId = t.tableId
                        WHERE wt.TableId IS NOT NULL AND wt.Status = 'Pending'
                        GROUP BY wt.TableId, wt.TableName, wt.TableNumber, t.status
                        ORDER BY ActiveOrders DESC";

                    using var reader = command.ExecuteReader();
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                    
                    while (reader.Read())
                    {
                        // Calculate total order value from cart items
                        decimal totalOrderValue = 0;
                        var allCartItemsStr = reader["AllCartItems"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(allCartItemsStr))
                        {
                            var cartItemsArray = allCartItemsStr.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var cartItemsJson in cartItemsArray)
                            {
                                var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(cartItemsJson, jsonOptions);
                                if (items != null)
                                {
                                    totalOrderValue += CalculateOrderTotal(items);
                                }
                            }
                        }
                        
                        summaries.Add(new TableOrderSummaryDto
                        {
                            TableId = reader["TableId"]?.ToString() ?? "",
                            TableName = reader["TableName"]?.ToString() ?? "",
                            TableNumber = Convert.ToInt32(reader["TableNumber"] ?? 0),
                            ActiveOrders = Convert.ToInt32(reader["ActiveOrders"] ?? 0),
                            FirstOrderTime = reader["FirstOrderTime"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["FirstOrderTime"]) 
                                : null,
                            LastOrderTime = reader["LastOrderTime"] != DBNull.Value 
                                ? Convert.ToDateTime(reader["LastOrderTime"]) 
                                : null,
                            Status = reader["TableStatus"]?.ToString() ?? "Available",
                            TotalOrderValue = totalOrderValue
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error getting table order summary: {ex.Message}");
                }

                return summaries;
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
