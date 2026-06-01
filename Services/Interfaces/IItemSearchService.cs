using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for item search and statistics operations
    /// </summary>
    public interface IItemSearchService
    {
        /// <summary>Get all active items (products, bundles, recipes) - DEPRECATED: Use GetAllItemsPaginatedAsync</summary>
        Task<List<ItemDto>> GetAllItemsAsync();

        /// <summary>Get all active items with pagination</summary>
        Task<ItemSearchResponseDto> GetAllItemsPaginatedAsync(int page = 1, int pageSize = 50);

        /// <summary>Get item by ID (works for products, bundles, recipes)</summary>
        Task<ItemDto?> GetItemByIdAsync(string itemId);

        /// <summary>Search items by name, SKU, or description</summary>
        Task<ItemSearchResponseDto> SearchItemsAsync(ItemSearchDto searchRequest);

        /// <summary>Get item statistics</summary>
        Task<ItemStatisticsDto> GetItemStatisticsAsync();

        /// <summary>Invalidate all item-related cache entries</summary>
        void InvalidateCache();
    }
}
