using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Text.Json;

namespace PottaAPI.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            // Try multiple possible database locations
            var possiblePaths = new[]
            {
                @"c:\Users\Thuram Jr\source\repos\PottaPOS\Potta Finance\Potta Finance\bin\Debug\net8.0-windows\pottadb.db",
                @"c:\Users\Thuram Jr\source\repos\PottaPOS\Potta Finance\Potta Finance\pottadb.db",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pottadb.db"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PottaPOS", "pottadb.db")
            };

            string dbPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    dbPath = path;
                    break;
                }
            }

            if (dbPath == null)
            {
                // If no existing database found, use the primary location
                dbPath = possiblePaths[0];
                Console.WriteLine($"Warning: Database not found. Will attempt to use: {dbPath}");
            }
            else
            {
                Console.WriteLine($"Database found at: {dbPath}");
            }

            _connectionString = $"Data Source={dbPath};Foreign Keys=True";
        }

        public async Task TestConnectionAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
            var tableCount = await command.ExecuteScalarAsync();
            
            Console.WriteLine($"Database connected successfully. Found {tableCount} tables.");
            
            // Ensure WaitingTransactions table has the correct schema
            await EnsureWaitingTransactionsSchemaAsync();
        }

        private async Task EnsureWaitingTransactionsSchemaAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            // First, check if the table exists and drop it if it has foreign key constraints
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='WaitingTransactions'";
            var existingSchema = await checkCommand.ExecuteScalarAsync() as string;
            
            // Add StaffId column if it doesn't exist to avoid data loss
            var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA table_info(WaitingTransactions)";
            using (var reader = await pragmaCommand.ExecuteReaderAsync())
            {
                var hasStaffId = false;
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals("StaffId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasStaffId = true;
                        break;
                    }
                }
                if (!hasStaffId)
                {
                    Console.WriteLine("Adding StaffId column to WaitingTransactions table...");
                    var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE WaitingTransactions ADD COLUMN StaffId INTEGER";
                    await alterCommand.ExecuteNonQueryAsync();
                }
            }
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS WaitingTransactions (
                    TransactionId TEXT PRIMARY KEY,
                    CartItems TEXT NOT NULL,
                    CustomerId TEXT,
                    TableId TEXT,
                    TableNumber INTEGER,
                    TableName TEXT,
                    StaffId INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                )";
            
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("WaitingTransactions table schema ensured without foreign key constraints.");
        }

        public async Task<List<MenuItemDto>> GetMenuItemsAsync()
        {
            var menuItems = new List<MenuItemDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT productId, name, sku, type, categories, description, 
                       cost, salesPrice, imageData, inventoryOnHand, 
                       taxable, taxId, status, hasVariations, variationCount
                FROM Products 
                WHERE status = 1
                ORDER BY name";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                menuItems.Add(new MenuItemDto
                {
                    ProductId = reader["productId"]?.ToString() ?? "",
                    Name = reader["name"]?.ToString() ?? "",
                    SKU = reader["sku"]?.ToString() ?? "",
                    Type = reader["type"]?.ToString() ?? "",
                    Categories = ParseCategories(reader["categories"]?.ToString()),
                    Description = reader["description"]?.ToString() ?? "",
                    Cost = Convert.ToDecimal(reader["cost"] ?? 0),
                    SalesPrice = Convert.ToDecimal(reader["salesPrice"] ?? 0),
                    ImageData = reader["imageData"] as byte[],
                    InventoryOnHand = Convert.ToInt32(reader["inventoryOnHand"] ?? 0),
                    Taxable = Convert.ToBoolean(reader["taxable"] ?? false),
                    TaxId = reader["taxId"]?.ToString(),
                    IsActive = Convert.ToBoolean(reader["status"] ?? true),
                    HasVariations = Convert.ToBoolean(reader["hasVariations"] ?? false),
                    VariationCount = Convert.ToInt32(reader["variationCount"] ?? 0)
                });
            }
            
            return menuItems;
        }

        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var categories = new List<CategoryDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT categoryId, categoryName, description, isActive
                FROM Categories 
                WHERE isActive = 1
                ORDER BY categoryName";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new CategoryDto
                {
                    CategoryId = reader["categoryId"]?.ToString() ?? "",
                    CategoryName = reader["categoryName"]?.ToString() ?? "",
                    Description = reader["description"]?.ToString() ?? "",
                    IsActive = Convert.ToBoolean(reader["isActive"] ?? true)
                });
            }
            
            return categories;
        }

        public async Task<List<BundleItemDto>> GetBundleItemsAsync()
        {
            var bundles = new List<BundleItemDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT b.bundleId, b.name, b.sku, b.structure, b.description,
                       b.cost, b.salesPrice, b.imageData, b.inventoryOnHand,
                       b.taxable, b.taxId, b.status
                FROM BundleItems b
                WHERE b.status = 1
                ORDER BY b.name";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bundle = new BundleItemDto
                {
                    BundleId = reader["bundleId"]?.ToString() ?? "",
                    Name = reader["name"]?.ToString() ?? "",
                    SKU = reader["sku"]?.ToString() ?? "",
                    Structure = reader["structure"]?.ToString() ?? "",
                    Description = reader["description"]?.ToString() ?? "",
                    Cost = Convert.ToDecimal(reader["cost"] ?? 0),
                    SalesPrice = Convert.ToDecimal(reader["salesPrice"] ?? 0),
                    ImageData = reader["imageData"] as byte[],
                    InventoryOnHand = Convert.ToInt32(reader["inventoryOnHand"] ?? 0),
                    Taxable = Convert.ToBoolean(reader["taxable"] ?? false),
                    TaxId = reader["taxId"]?.ToString(),
                    IsActive = Convert.ToBoolean(reader["status"] ?? true),
                    Components = new List<BundleComponentDto>()
                };
                
                // Get bundle components
                bundle.Components = await GetBundleComponentsAsync(bundle.BundleId);
                bundles.Add(bundle);
            }
            
            return bundles;
        }

        public async Task<List<ProductVariationDto>> GetProductVariationsAsync()
        {
            var variations = new List<ProductVariationDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT variationId, parentProductId, sku, name, cost, salesPrice,
                       inventoryOnHand, reorderPoint, imageData, status
                FROM ProductVariations 
                WHERE status = 1
                ORDER BY parentProductId, name";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                variations.Add(new ProductVariationDto
                {
                    VariationId = reader["variationId"]?.ToString() ?? "",
                    ParentProductId = reader["parentProductId"]?.ToString() ?? "",
                    SKU = reader["sku"]?.ToString() ?? "",
                    Name = reader["name"]?.ToString() ?? "",
                    Cost = Convert.ToDecimal(reader["cost"] ?? 0),
                    SalesPrice = Convert.ToDecimal(reader["salesPrice"] ?? 0),
                    InventoryOnHand = Convert.ToInt32(reader["inventoryOnHand"] ?? 0),
                    ReorderPoint = Convert.ToInt32(reader["reorderPoint"] ?? 0),
                    ImageData = reader["imageData"] as byte[],
                    IsActive = Convert.ToBoolean(reader["status"] ?? true)
                });
            }
            
            return variations;
        }

        public async Task<List<StaffDto>> GetActiveStaffAsync()
        {
            var staff = new List<StaffDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, FirstName, LastName, Email, Phone, DailyCode, 
                       CodeGeneratedDate, CreatedDate, IsActive
                FROM Staff 
                WHERE IsActive = 1
                ORDER BY FirstName, LastName";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                staff.Add(new StaffDto
                {
                    Id = Convert.ToInt32(reader["Id"] ?? 0),
                    FirstName = reader["FirstName"]?.ToString() ?? "",
                    LastName = reader["LastName"]?.ToString() ?? "",
                    Email = reader["Email"]?.ToString() ?? "",
                    Phone = reader["Phone"]?.ToString() ?? "",
                    DailyCode = reader["DailyCode"]?.ToString() ?? "",
                    CodeGeneratedDate = Convert.ToDateTime(reader["CodeGeneratedDate"]),
                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                    IsActive = Convert.ToBoolean(reader["IsActive"] ?? true)
                });
            }
            
            return staff;
        }

        public async Task<StaffDto?> ValidateStaffCodeAsync(string dailyCode)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, FirstName, LastName, Email, Phone, DailyCode, 
                       CodeGeneratedDate, CreatedDate, IsActive
                FROM Staff 
                WHERE DailyCode = @code AND IsActive = 1";
            command.Parameters.AddWithValue("@code", dailyCode);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new StaffDto
                {
                    Id = Convert.ToInt32(reader["Id"] ?? 0),
                    FirstName = reader["FirstName"]?.ToString() ?? "",
                    LastName = reader["LastName"]?.ToString() ?? "",
                    Email = reader["Email"]?.ToString() ?? "",
                    Phone = reader["Phone"]?.ToString() ?? "",
                    DailyCode = reader["DailyCode"]?.ToString() ?? "",
                    CodeGeneratedDate = Convert.ToDateTime(reader["CodeGeneratedDate"]),
                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                    IsActive = Convert.ToBoolean(reader["IsActive"] ?? true)
                };
            }
            
            return null;
        }

        public async Task<List<TableDto>> GetTablesAsync()
        {
            var tables = new List<TableDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT tableId, tableName, tableNumber, capacity, status,
                       currentCustomerId, currentTransactionId, description, isActive
                FROM Tables 
                WHERE isActive = 1
                ORDER BY tableNumber";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new TableDto
                {
                    TableId = reader["tableId"]?.ToString() ?? "",
                    TableName = reader["tableName"]?.ToString() ?? "",
                    TableNumber = Convert.ToInt32(reader["tableNumber"] ?? 0),
                    Capacity = Convert.ToInt32(reader["capacity"] ?? 4),
                    Status = reader["status"]?.ToString() ?? "Available",
                    CurrentCustomerId = reader["currentCustomerId"]?.ToString(),
                    CurrentTransactionId = reader["currentTransactionId"]?.ToString(),
                    Description = reader["description"]?.ToString() ?? "",
                    IsActive = Convert.ToBoolean(reader["isActive"] ?? true)
                });
            }
            
            return tables;
        }

        public async Task<bool> UpdateTableStatusAsync(string tableId, string status, string? customerId = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tables 
                SET status = @status, 
                    currentCustomerId = @customerId,
                    modifiedDate = CURRENT_TIMESTAMP
                WHERE tableId = @tableId";
            
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@customerId", customerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@tableId", tableId);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task<List<CustomerDto>> GetCustomersAsync()
        {
            var customers = new List<CustomerDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT customerId, firstName, lastName, email, phone, 
                       address, city, state, country, isActive
                FROM Customer 
                WHERE isActive = 1
                ORDER BY firstName, lastName";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(new CustomerDto
                {
                    CustomerId = reader["customerId"]?.ToString() ?? "",
                    FirstName = reader["firstName"]?.ToString() ?? "",
                    LastName = reader["lastName"]?.ToString() ?? "",
                    Email = reader["email"]?.ToString() ?? "",
                    Phone = reader["phone"]?.ToString() ?? "",
                    Address = reader["address"]?.ToString() ?? "",
                    City = reader["city"]?.ToString() ?? "",
                    State = reader["state"]?.ToString() ?? "",
                    Country = reader["country"]?.ToString() ?? "",
                    IsActive = Convert.ToBoolean(reader["isActive"] ?? true)
                });
            }
            
            return customers;
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(string customerId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT customerId, firstName, lastName, email, phone, 
                       address, city, state, country, isActive
                FROM Customer 
                WHERE customerId = @customerId AND isActive = 1";
            command.Parameters.AddWithValue("@customerId", customerId);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CustomerDto
                {
                    CustomerId = reader["customerId"]?.ToString() ?? "",
                    FirstName = reader["firstName"]?.ToString() ?? "",
                    LastName = reader["lastName"]?.ToString() ?? "",
                    Email = reader["email"]?.ToString() ?? "",
                    Phone = reader["phone"]?.ToString() ?? "",
                    Address = reader["address"]?.ToString() ?? "",
                    City = reader["city"]?.ToString() ?? "",
                    State = reader["state"]?.ToString() ?? "",
                    Country = reader["country"]?.ToString() ?? "",
                    IsActive = Convert.ToBoolean(reader["isActive"] ?? true)
                };
            }
            
            return null;
        }

        public async Task<string> CreateWaitingTransactionAsync(CreateWaitingTransactionDto transaction)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Generate transaction ID like the POS system does
                    var transactionId = "M" + DateTime.Now.ToString("yyyyMMddHHmmss");

                    var sql = @"
                        INSERT INTO WaitingTransactions (TransactionId, CartItems, CustomerId, TableId, TableNumber, TableName, StaffId, CreatedDate, ModifiedDate) 
                        VALUES (@TransactionId, @CartItems, @CustomerId, @TableId, @TableNumber, @TableName, @StaffId, @CreatedDate, @ModifiedDate)";

                    var parameters = new SqliteParameter[]
                    {
                        new SqliteParameter("@TransactionId", transactionId),
                        new SqliteParameter("@CartItems", JsonSerializer.Serialize(transaction.Items ?? new List<WaitingTransactionItemDto>())),
                        new SqliteParameter("@CustomerId", transaction.CustomerId ?? (object)DBNull.Value),
                        new SqliteParameter("@TableId", transaction.TableId ?? (object)DBNull.Value),
                        new SqliteParameter("@TableNumber", transaction.TableNumber ?? (object)DBNull.Value),
                        new SqliteParameter("@TableName", transaction.TableName ?? (object)DBNull.Value),
                        new SqliteParameter("@StaffId", transaction.StaffId),
                        new SqliteParameter("@CreatedDate", DateTime.Now),
                        new SqliteParameter("@ModifiedDate", DateTime.Now)
                    };

                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);
                    
                    int result = command.ExecuteNonQuery();
                    Console.WriteLine($"Waiting transaction saved (ID: {transactionId}). Rows affected: {result}");
                    return transactionId;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving waiting transaction: {ex.Message}");
                    throw new Exception($"Failed to save waiting transaction: {ex.Message}");
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
                        SELECT TransactionId, CartItems, CustomerId, TableId, TableNumber, TableName, StaffId, CreatedDate, ModifiedDate
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

                    Console.WriteLine($"Retrieved waiting transactions from database");

                    while (reader.Read())
                    {
                        try
                        {
                            var customerId = reader["CustomerId"] != DBNull.Value && !string.IsNullOrEmpty(reader["CustomerId"]?.ToString())
                                ? reader["CustomerId"].ToString()
                                : null;

                            var itemsJson = reader["CartItems"]?.ToString() ?? "[]";
                            var items = JsonSerializer.Deserialize<List<WaitingTransactionItemDto>>(itemsJson) ?? new List<WaitingTransactionItemDto>();

                            var transaction = new WaitingTransactionDto
                            {
                                TransactionId = reader["TransactionId"]?.ToString() ?? "",
                                CustomerId = customerId,
                                TableId = reader["TableId"] != DBNull.Value ? reader["TableId"]?.ToString() : null,
                                TableNumber = reader["TableNumber"] != DBNull.Value ? Convert.ToInt32(reader["TableNumber"]) : null,
                                TableName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                StaffId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                CreatedDate = reader.GetDateTime(7),
                                ModifiedDate = reader.GetDateTime(8),
                                Status = "Pending"
                            };

                            if (!string.IsNullOrEmpty(transaction.TransactionId))
                            {
                                transactions.Add(transaction);
                                Console.WriteLine($"Loaded waiting transaction: ID={transaction.TransactionId}, CustomerId={customerId}, TableId={transaction.TableId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating waiting transaction from row: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting all waiting transactions: {ex.Message}");
                    throw new Exception($"Failed to get waiting transactions: {ex.Message}");
                }

                Console.WriteLine($"Returning {transactions.Count} valid waiting transactions");
                return transactions;
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
                        SET ModifiedDate = @modifiedDate 
                        WHERE TransactionId = @transactionId";

                    var parameters = new SqliteParameter[]
                    {
                        new SqliteParameter("@modifiedDate", DateTime.Now),
                        new SqliteParameter("@transactionId", transactionId)
                    };

                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    int result = command.ExecuteNonQuery();
                    Console.WriteLine($"Waiting transaction updated (ID: {transactionId}). Rows affected: {result}");
                    return result > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating waiting transaction (ID: {transactionId}): {ex.Message}");
                    throw new Exception($"Failed to update waiting transaction: {ex.Message}");
                }
            });
        }

        public async Task<bool> DeleteWaitingTransactionAsync(string transactionId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sql = "DELETE FROM WaitingTransactions WHERE TransactionId = @transactionId";
                    var parameters = new SqliteParameter[]
                    {
                        new SqliteParameter("@transactionId", transactionId)
                    };

                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    int result = command.ExecuteNonQuery();
                    Console.WriteLine($"Waiting transaction deleted. Rows affected: {result}");
                    return result > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting waiting transaction: {ex.Message}");
                    throw new Exception($"Failed to delete waiting transaction: {ex.Message}");
                }
            });
        }

        public async Task<SyncInfoDto> GetLastSyncInfoAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    (SELECT COUNT(*) FROM Products WHERE status = 1) as ProductCount,
                    (SELECT COUNT(*) FROM BundleItems WHERE status = 1) as BundleCount,
                    (SELECT COUNT(*) FROM ProductVariations WHERE status = 1) as VariationCount,
                    (SELECT COUNT(*) FROM Categories WHERE isActive = 1) as CategoryCount,
                    (SELECT COUNT(*) FROM Tables WHERE isActive = 1) as TableCount,
                    (SELECT COUNT(*) FROM Staff WHERE IsActive = 1) as StaffCount,
                    (SELECT COUNT(*) FROM Customer WHERE isActive = 1) as CustomerCount,
                    (SELECT COUNT(*) FROM WaitingTransactions) as WaitingTransactionCount,
                    CURRENT_TIMESTAMP as LastSync";
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new SyncInfoDto
                {
                    ProductCount = Convert.ToInt32(reader["ProductCount"] ?? 0),
                    BundleCount = Convert.ToInt32(reader["BundleCount"] ?? 0),
                    VariationCount = Convert.ToInt32(reader["VariationCount"] ?? 0),
                    CategoryCount = Convert.ToInt32(reader["CategoryCount"] ?? 0),
                    TableCount = Convert.ToInt32(reader["TableCount"] ?? 0),
                    StaffCount = Convert.ToInt32(reader["StaffCount"] ?? 0),
                    CustomerCount = Convert.ToInt32(reader["CustomerCount"] ?? 0),
                    WaitingTransactionCount = Convert.ToInt32(reader["WaitingTransactionCount"] ?? 0),
                    LastSync = DateTime.Parse(reader["LastSync"]?.ToString() ?? DateTime.Now.ToString())
                };
            }
            
            return new SyncInfoDto();
        }

        private async Task<List<BundleComponentDto>> GetBundleComponentsAsync(string bundleId)
        {
            var components = new List<BundleComponentDto>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT bc.productId, bc.quantity, p.name as productName, p.salesPrice
                FROM BundleComponents bc
                INNER JOIN Products p ON bc.productId = p.productId
                WHERE bc.bundleId = @bundleId AND p.status = 1";
            command.Parameters.AddWithValue("@bundleId", bundleId);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                components.Add(new BundleComponentDto
                {
                    ProductId = reader["productId"]?.ToString() ?? "",
                    ProductName = reader["productName"]?.ToString() ?? "",
                    Quantity = Convert.ToInt32(reader["quantity"] ?? 1),
                    Price = Convert.ToDecimal(reader["salesPrice"] ?? 0)
                });
            }
            
            return components;
        }

        private List<string> ParseCategories(string? categoriesJson)
        {
            if (string.IsNullOrEmpty(categoriesJson))
                return new List<string>();
            
            try
            {
                return JsonSerializer.Deserialize<List<string>>(categoriesJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
