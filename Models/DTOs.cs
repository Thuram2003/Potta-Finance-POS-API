namespace PottaAPI.Models
{
    public class SyncInfoDto
    {
        public int ProductCount { get; set; }
        public int BundleCount { get; set; }
        public int VariationCount { get; set; }
        public int CategoryCount { get; set; }
        public int TableCount { get; set; }
        public int StaffCount { get; set; }
        public int CustomerCount { get; set; }
        public int WaitingTransactionCount { get; set; }
        public DateTime LastSync { get; set; }

        // Computed properties for additional insights
        public int TotalItems => ProductCount + BundleCount + VariationCount;
        public int TotalEntities => ProductCount + BundleCount + VariationCount + CategoryCount + TableCount + StaffCount + CustomerCount;
        public string SyncAge => GetSyncAge();

        private string GetSyncAge()
        {
            var age = DateTime.Now - LastSync;
            if (age.TotalMinutes < 1) return "Just now";
            if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes} minute(s) ago";
            if (age.TotalHours < 24) return $"{(int)age.TotalHours} hour(s) ago";
            return $"{(int)age.TotalDays} day(s) ago";
        }
    }

    public class DetailedSyncInfoDto : SyncInfoDto
    {
        public DatabaseStatistics DatabaseStats { get; set; } = new();
        public InventoryStatistics InventoryStats { get; set; } = new();
        public TableStatistics TableStats { get; set; } = new();
        public TransactionStatistics TransactionStats { get; set; } = new();
    }

    public class DatabaseStatistics
    {
        public long DatabaseSizeBytes { get; set; }
        public string DatabaseSizeFormatted { get; set; } = "";
        public string DatabasePath { get; set; } = "";
        public int TotalTables { get; set; }
        public DateTime DatabaseCreatedDate { get; set; }
        public DateTime DatabaseModifiedDate { get; set; }
    }

    public class InventoryStatistics
    {
        public int LowStockItems { get; set; }
        public int OutOfStockItems { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public int TaxableItems { get; set; }
        public int NonTaxableItems { get; set; }
    }

    public class TableStatistics
    {
        public int AvailableTables { get; set; }
        public int OccupiedTables { get; set; }
        public int ReservedTables { get; set; }
        public double OccupancyRate { get; set; }
    }

    public class TransactionStatistics
    {
        public int PendingTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public decimal TotalTransactionValue { get; set; }
        public DateTime? OldestPendingTransaction { get; set; }
    }

    public class SyncDataDto
    {
        public List<BundleDto> BundleItems { get; set; } = new();
        public List<ProductVariationDto> ProductVariations { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<TableDTO> Tables { get; set; } = new(); 
        public List<StaffDTO> Staff { get; set; } = new(); 
        public List<CustomerDto> Customers { get; set; } = new();
        public SyncInfoDto SyncInfo { get; set; } = new();
    }

    // Health DTOs
    public class DatabaseHealthDto
    {
        public bool Connected { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public DatabaseHealthStatistics? Statistics { get; set; }
        public DateTime? LastSync { get; set; }
        public string ResponseTime { get; set; } = "";
        public string? Error { get; set; }
        public string? Details { get; set; }
    }

    public class DatabaseHealthStatistics
    {
        public int Products { get; set; }
        public int Bundles { get; set; }
        public int Variations { get; set; }
        public int Categories { get; set; }
        public int Tables { get; set; }
        public int Staff { get; set; }
        public int Customers { get; set; }
        public int WaitingTransactions { get; set; }
        public int TotalItems { get; set; }
    }

}
