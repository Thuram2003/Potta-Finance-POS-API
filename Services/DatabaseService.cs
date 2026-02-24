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
            string dbPath = FindDatabasePath();

            if (dbPath == null)
            {
                throw new FileNotFoundException(
                    "Database file 'pottadb.db' not found. Please ensure the POS application is installed correctly. " +
                    "The database should be located in the same directory as the application executables.");
            }

            Console.WriteLine($"Database found at: {dbPath}");
            _connectionString = $"Data Source={dbPath};Foreign Keys=True;Mode=ReadWrite";
        }

        private string FindDatabasePath()
        {
            // Get the base directory where the API is running from
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Check if we're in Debug mode (development environment)
            var isDebugMode = baseDirectory.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase);

            List<string> possiblePaths = new List<string>();

            if (isDebugMode)
            {
                // Debug mode: Look in the WPF app's debug output directory
                // Navigate from API bin folder to WPF app bin folder
                var debugPath = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\..\Potta Finance\bin\Debug\net8.0-windows\pottadb.db"));
                possiblePaths.Add(debugPath);
                Console.WriteLine($"Running in DEBUG mode. Looking for database at: {debugPath}");
            }
            else
            {
                // Production mode: Database is in the same directory as the executables
                // When installed via Inno Setup, both PottaAPI.exe and Potta Finance.exe are in the same folder
                possiblePaths.Add(Path.Combine(baseDirectory, "pottadb.db"));

                // Also check parent directory (in case API is in a subfolder)
                var parentDir = Directory.GetParent(baseDirectory)?.FullName;
                if (parentDir != null)
                {
                    possiblePaths.Add(Path.Combine(parentDir, "pottadb.db"));
                }

                // Check common installation paths
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                possiblePaths.Add(Path.Combine(localAppData, "Programs", "Potta Finance POS", "pottadb.db"));
                possiblePaths.Add(Path.Combine(localAppData, "Potta Finance POS", "pottadb.db"));

                Console.WriteLine($"Running in PRODUCTION mode. Searching for database...");
            }

            // Search for the database file
            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"Checking: {path}");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Log all attempted paths for debugging
            Console.WriteLine("Database not found in any of the following locations:");
            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"  - {path}");
            }

            return null;
        }

        public async Task TestConnectionAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
                var tableCount = await command.ExecuteScalarAsync();

                Console.WriteLine($"Database connected successfully. Found {tableCount} tables.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to connect to database: {ex.Message}");
                throw new Exception($"Database connection failed. Please ensure the Potta Finance POS application is installed and the database exists. Error: {ex.Message}");
            }
        }

        // NOTE: Staff operations moved to StaffService.cs
        // NOTE: Table operations moved to TableService.cs

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

        public async Task<DetailedSyncInfoDto> GetDetailedSyncInfoAsync()
        {
            var basicInfo = await GetLastSyncInfoAsync();

            return new DetailedSyncInfoDto
            {
                ProductCount = basicInfo.ProductCount,
                BundleCount = basicInfo.BundleCount,
                VariationCount = basicInfo.VariationCount,
                CategoryCount = basicInfo.CategoryCount,
                TableCount = basicInfo.TableCount,
                StaffCount = basicInfo.StaffCount,
                CustomerCount = basicInfo.CustomerCount,
                WaitingTransactionCount = basicInfo.WaitingTransactionCount,
                LastSync = basicInfo.LastSync,
                DatabaseStats = await GetDatabaseStatisticsAsync(),
                InventoryStats = await GetInventoryStatisticsAsync(),
                TableStats = await GetTableStatisticsAsync(),
                TransactionStats = new TransactionStatistics
                {
                    PendingTransactions = basicInfo.WaitingTransactionCount,
                    CompletedTransactions = 0,
                    TotalTransactionValue = 0,
                    OldestPendingTransaction = await GetOldestPendingTransactionDateAsync()
                }
            };
        }

        public async Task<DatabaseStatistics> GetDatabaseStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get database file info
            var dbPath = connection.DataSource;
            var fileInfo = new FileInfo(dbPath);

            // Get table count
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

            return new DatabaseStatistics
            {
                DatabaseSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                DatabaseSizeFormatted = FormatBytes(fileInfo.Exists ? fileInfo.Length : 0),
                DatabasePath = dbPath,
                TotalTables = tableCount,
                DatabaseCreatedDate = fileInfo.Exists ? fileInfo.CreationTime : DateTime.MinValue,
                DatabaseModifiedDate = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue
            };
        }

        public async Task<InventoryStatistics> GetInventoryStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(CASE WHEN inventoryOnHand <= 10 AND inventoryOnHand > 0 THEN 1 END) as LowStock,
                    COUNT(CASE WHEN inventoryOnHand = 0 THEN 1 END) as OutOfStock,
                    COALESCE(SUM(CASE WHEN status = 1 THEN cost * inventoryOnHand ELSE 0 END), 0) as TotalValue,
                    COUNT(CASE WHEN taxable = 1 AND status = 1 THEN 1 END) as TaxableCount,
                    COUNT(CASE WHEN taxable = 0 AND status = 1 THEN 1 END) as NonTaxableCount
                FROM Products
                WHERE status = 1";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new InventoryStatistics
                {
                    LowStockItems = Convert.ToInt32(reader["LowStock"] ?? 0),
                    OutOfStockItems = Convert.ToInt32(reader["OutOfStock"] ?? 0),
                    TotalInventoryValue = Convert.ToDecimal(reader["TotalValue"] ?? 0),
                    TaxableItems = Convert.ToInt32(reader["TaxableCount"] ?? 0),
                    NonTaxableItems = Convert.ToInt32(reader["NonTaxableCount"] ?? 0)
                };
            }

            return new InventoryStatistics();
        }

        public async Task<TableStatistics> GetTableStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(CASE WHEN status = 'Available' THEN 1 END) as Available,
                    COUNT(CASE WHEN status = 'Occupied' THEN 1 END) as Occupied,
                    COUNT(CASE WHEN status = 'Reserved' THEN 1 END) as Reserved,
                    COUNT(*) as Total
                FROM Tables
                WHERE isActive = 1";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var available = Convert.ToInt32(reader["Available"] ?? 0);
                var occupied = Convert.ToInt32(reader["Occupied"] ?? 0);
                var reserved = Convert.ToInt32(reader["Reserved"] ?? 0);
                var total = Convert.ToInt32(reader["Total"] ?? 0);

                return new TableStatistics
                {
                    AvailableTables = available,
                    OccupiedTables = occupied,
                    ReservedTables = reserved,
                    OccupancyRate = total > 0 ? (double)occupied / total * 100 : 0
                };
            }

            return new TableStatistics();
        }

        private async Task<DateTime?> GetOldestPendingTransactionDateAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT MIN(CreatedDate) FROM WaitingTransactions";
            var result = await command.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                return Convert.ToDateTime(result);
            }

            return null;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Get the connection string for direct database access
        /// </summary>
        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}
