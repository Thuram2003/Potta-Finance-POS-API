using PottaAPI.Models;

namespace PottaAPI.Services
{
    public interface IDatabaseService
    {
        Task TestConnectionAsync();
        
        // Menu/Products
        Task<List<MenuItemDto>> GetMenuItemsAsync();
        Task<List<CategoryDto>> GetCategoriesAsync();
        Task<List<BundleItemDto>> GetBundleItemsAsync();
        Task<List<ProductVariationDto>> GetProductVariationsAsync();
        
        // Staff & Authentication
        Task<List<StaffDto>> GetActiveStaffAsync();
        Task<StaffDto?> ValidateStaffCodeAsync(string dailyCode);
        
        // Tables
        Task<List<TableDto>> GetTablesAsync();
        Task<bool> UpdateTableStatusAsync(string tableId, string status, string? customerId = null);
        
        // Customers
        Task<List<CustomerDto>> GetCustomersAsync();
        Task<CustomerDto?> GetCustomerByIdAsync(string customerId);
        
        // Orders/Transactions
        Task<string> CreateWaitingTransactionAsync(CreateWaitingTransactionDto transaction);
        Task<List<WaitingTransactionDto>> GetWaitingTransactionsAsync(int? staffId = null);
        Task<bool> UpdateWaitingTransactionStatusAsync(string transactionId, string status);
        Task<bool> DeleteWaitingTransactionAsync(string transactionId);
        
        // Sync timestamps for mobile apps
        Task<SyncInfoDto> GetLastSyncInfoAsync();
    }
}
