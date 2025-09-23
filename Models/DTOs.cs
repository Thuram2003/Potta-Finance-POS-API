namespace PottaAPI.Models
{
    // Menu/Product DTOs
    public class MenuItemDto
    {
        public string ProductId { get; set; } = "";
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string Type { get; set; } = "";
        public List<string> Categories { get; set; } = new();
        public string Description { get; set; } = "";
        public decimal Cost { get; set; }
        public decimal SalesPrice { get; set; }
        public byte[]? ImageData { get; set; }
        public int InventoryOnHand { get; set; }
        public bool Taxable { get; set; }
        public string? TaxId { get; set; }
        public bool IsActive { get; set; }
        public bool HasVariations { get; set; }
        public int VariationCount { get; set; }
    }

    public class CategoryDto
    {
        public string CategoryId { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class BundleItemDto
    {
        public string BundleId { get; set; } = "";
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string Structure { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Cost { get; set; }
        public decimal SalesPrice { get; set; }
        public byte[]? ImageData { get; set; }
        public int InventoryOnHand { get; set; }
        public bool Taxable { get; set; }
        public string? TaxId { get; set; }
        public bool IsActive { get; set; }
        public List<BundleComponentDto> Components { get; set; } = new();
    }

    public class BundleComponentDto
    {
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class ProductVariationDto
    {
        public string VariationId { get; set; } = "";
        public string ParentProductId { get; set; } = "";
        public string SKU { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Cost { get; set; }
        public decimal SalesPrice { get; set; }
        public int InventoryOnHand { get; set; }
        public int ReorderPoint { get; set; }
        public byte[]? ImageData { get; set; }
        public bool IsActive { get; set; }
    }

    // Staff DTOs
    public class StaffDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string DailyCode { get; set; } = "";
        public DateTime CodeGeneratedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsCodeExpired => DateTime.Now - CodeGeneratedDate > TimeSpan.FromHours(24);
    }

    public class StaffLoginDto
    {
        public string DailyCode { get; set; } = "";
    }

    public class StaffLoginResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public StaffDto? Staff { get; set; }
    }

    // Table DTOs
    public class TableDto
    {
        public string TableId { get; set; } = "";
        public string TableName { get; set; } = "";
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string Status { get; set; } = "";
        public string? CurrentCustomerId { get; set; }
        public string? CurrentTransactionId { get; set; }
        public string Description { get; set; } = "";
        public bool IsActive { get; set; }
        public string DisplayName => !string.IsNullOrEmpty(TableName) ? TableName : $"Table {TableNumber}";
        public bool IsAvailable => Status == "Available";
    }

    public class UpdateTableStatusDto
    {
        public string Status { get; set; } = "";
        public string? CustomerId { get; set; }
    }

    // Customer DTOs
    public class CustomerDto
    {
        public string CustomerId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public bool IsActive { get; set; }
    }

    // Transaction DTOs
    public class WaitingTransactionDto
    {
        public string TransactionId { get; set; } = "";
        public string? CustomerId { get; set; }
        public string? TableId { get; set; }
        public int? TableNumber { get; set; }
        public string? TableName { get; set; }
        public int? StaffId { get; set; }
        public List<WaitingTransactionItemDto> Items { get; set; } = new();
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class CreateWaitingTransactionDto
    {
        public string? CustomerId { get; set; }
        public string? TableId { get; set; }
        public int? TableNumber { get; set; }
        public string? TableName { get; set; }
        public int StaffId { get; set; }
        public List<WaitingTransactionItemDto> Items { get; set; } = new();
    }

    public class WaitingTransactionItemDto
    {
        public string Name { get; set; } = "";
        public string ProductId { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; } = 0;
        public decimal Total { get; set; }
        public decimal TaxAmount { get; set; } = 0;
        public bool Taxable { get; set; } = false;
        public string TaxId { get; set; } = "";
        public decimal SubTotal => (Price * Quantity) - Discount;
    }

    public class UpdateTransactionStatusDto
    {
        public string Status { get; set; } = "";
    }

    // Sync DTOs
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
    }

    public class SyncDataDto
    {
        public List<MenuItemDto> MenuItems { get; set; } = new();
        public List<BundleItemDto> BundleItems { get; set; } = new();
        public List<ProductVariationDto> ProductVariations { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<TableDto> Tables { get; set; } = new();
        public List<StaffDto> Staff { get; set; } = new();
        public List<CustomerDto> Customers { get; set; } = new();
        public SyncInfoDto SyncInfo { get; set; } = new();
    }

    // API Response DTOs
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ErrorResponseDto
    {
        public string Error { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
