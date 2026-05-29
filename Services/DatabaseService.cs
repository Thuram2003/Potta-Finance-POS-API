using Dapper;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;
using System.Text.Json;

namespace PottaAPI.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConnectionStringProvider connectionStringProvider)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            Console.WriteLine($"DatabaseService initialized with connection string");
        }

        public async Task TestConnectionAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var tableCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table'");

                Console.WriteLine($"Database connected successfully. Found {tableCount} tables.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to connect to database: {ex.Message}");
                throw new Exception($"Database connection failed. Please ensure the Potta Finance POS application is installed and the database exists. Error: {ex.Message}");
            }
        }

        public async Task<SyncInfoDto> GetLastSyncInfoAsync()
        {
            using var connection = new SqliteConnection(_connectionString);

            var sql = @"
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

            return await connection.QueryFirstOrDefaultAsync < SyncInfoDto > (sql) ?? new SyncInfoDto();
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

            var tableCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table'");

            var dbPath = connection.DataSource;
            var fileInfo = new FileInfo(dbPath);

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

            var sql = @"
        SELECT 
            COUNT(CASE WHEN inventoryOnHand <= 10 AND inventoryOnHand > 0 THEN 1 END) as LowStockItems,
            COUNT(CASE WHEN inventoryOnHand = 0 THEN 1 END) as OutOfStockItems,
            COALESCE(SUM(CASE WHEN status = 1 THEN cost * inventoryOnHand ELSE 0 END), 0) as TotalInventoryValue,
            COUNT(CASE WHEN taxable = 1 AND status = 1 THEN 1 END) as TaxableItems,
            COUNT(CASE WHEN taxable = 0 AND status = 1 THEN 1 END) as NonTaxableItems
        FROM Products
        WHERE status = 1";

            // Dapper maps column aliases directly to property names
            return await connection.QueryFirstOrDefaultAsync < InventoryStatistics > (sql) ?? new InventoryStatistics();
        }

        public async Task<TableStatistics> GetTableStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);

            var sql = @"
        SELECT 
            COUNT(CASE WHEN status = 'Available' THEN 1 END) as AvailableTables,
            COUNT(CASE WHEN status = 'Occupied' THEN 1 END) as OccupiedTables,
            COUNT(CASE WHEN status = 'Reserved' THEN 1 END) as ReservedTables,
            COUNT(*) as TotalTables
        FROM Tables
        WHERE isActive = 1";

            var result = await connection.QueryFirstOrDefaultAsync < TableStatsRaw > (sql) ?? new TableStatsRaw();

            return new TableStatistics
            {
                AvailableTables = result.AvailableTables,
                OccupiedTables = result.OccupiedTables,
                ReservedTables = result.ReservedTables,
                OccupancyRate = result.TotalTables > 0 ? (double)result.OccupiedTables / result.TotalTables * 100 : 0
            };
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

        private class TableStatsRaw
        {
            public int AvailableTables { get; set; }
            public int OccupiedTables { get; set; }
            public int ReservedTables { get; set; }
            public int TotalTables { get; set; }
        }
    }
}
