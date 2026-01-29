using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for item-related database operations (products, bundles, recipes, variations)
    /// </summary>
    public interface IItemService
    {
        #region General Item Operations
        
        /// <summary>
        /// Get all active items (products, bundles, recipes)
        /// </summary>
        /// <returns>List of active items</returns>
        Task<List<ItemDto>> GetAllItemsAsync();

        /// <summary>
        /// Get item by ID (works for products, bundles, recipes)
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Item if found, null otherwise</returns>
        Task<ItemDto?> GetItemByIdAsync(string itemId);

        /// <summary>
        /// Search items by name, SKU, or description
        /// </summary>
        /// <param name="searchRequest">Search criteria</param>
        /// <returns>Search results with pagination</returns>
        Task<ItemSearchResponseDto> SearchItemsAsync(ItemSearchDto searchRequest);

        /// <summary>
        /// Get item statistics
        /// </summary>
        /// <returns>Item statistics</returns>
        Task<ItemStatisticsDto> GetItemStatisticsAsync();

        #endregion

        #region Product Operations

        /// <summary>
        /// Get all active products
        /// </summary>
        /// <returns>List of active products</returns>
        Task<List<ProductDto>> GetAllProductsAsync();

        /// <summary>
        /// Get product by ID with variations
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>Product with variations if found, null otherwise</returns>
        Task<ProductDto?> GetProductByIdAsync(string productId);

        /// <summary>
        /// Get products by category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>List of products in category</returns>
        Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId);

        /// <summary>
        /// Get low stock products
        /// </summary>
        /// <returns>List of products with low stock</returns>
        Task<List<ProductDto>> GetLowStockProductsAsync();

        #endregion

        #region Product Variation Operations

        /// <summary>
        /// Get variations for a product
        /// </summary>
        /// <param name="productId">Parent product ID</param>
        /// <returns>List of product variations</returns>
        Task<List<ProductVariationDto>> GetProductVariationsAsync(string productId);

        /// <summary>
        /// Get variation by ID
        /// </summary>
        /// <param name="variationId">Variation ID</param>
        /// <returns>Variation if found, null otherwise</returns>
        Task<ProductVariationDto?> GetVariationByIdAsync(string variationId);

        #endregion

        #region Bundle Operations

        /// <summary>
        /// Get all active bundles
        /// </summary>
        /// <returns>List of active bundles</returns>
        Task<List<BundleDto>> GetAllBundlesAsync();

        /// <summary>
        /// Get bundle by ID with components
        /// </summary>
        /// <param name="bundleId">Bundle ID</param>
        /// <returns>Bundle with components if found, null otherwise</returns>
        Task<BundleDto?> GetBundleByIdAsync(string bundleId);

        /// <summary>
        /// Get all recipes
        /// </summary>
        /// <returns>List of recipes</returns>
        Task<List<BundleDto>> GetAllRecipesAsync();

        #endregion

        #region Category Operations

        /// <summary>
        /// Get all categories with item counts
        /// </summary>
        /// <returns>List of categories</returns>
        Task<List<CategoryDto>> GetAllCategoriesAsync();

        /// <summary>
        /// Get category by ID
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>Category if found, null otherwise</returns>
        Task<CategoryDto?> GetCategoryByIdAsync(string categoryId);

        #endregion
    }
}