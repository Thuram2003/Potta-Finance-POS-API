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
        Task<List<ItemDto>> GetAllItemsAsync();

        /// <summary>
        /// Get item by ID (works for products, bundles, recipes)
        /// </summary>
        Task<ItemDto?> GetItemByIdAsync(string itemId);

        /// <summary>
        /// Search items by name, SKU, or description
        /// </summary>
        Task<ItemSearchResponseDto> SearchItemsAsync(ItemSearchDto searchRequest);

        /// <summary>
        /// Get item statistics
        /// </summary>
        Task<ItemStatisticsDto> GetItemStatisticsAsync();

        #endregion

        #region Product Operations

        /// <summary>
        /// Get all active products
        /// </summary>
        Task<List<ProductDto>> GetAllProductsAsync();

        /// <summary>
        /// Get product by ID with variations
        /// </summary>
        Task<ProductDto?> GetProductByIdAsync(string productId);

        /// <summary>
        /// Get products by category
        /// </summary>
        Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId);

        /// <summary>
        /// Get low stock products
        /// </summary>
        Task<List<ProductDto>> GetLowStockProductsAsync();

        #endregion

        #region Product Variation Operations

        /// <summary>
        /// Get variations for a product with full attribute and value data (for ComboBox UI)
        /// </summary>
        Task<ProductVariationsWithAttributesDto> GetProductVariationsWithAttributesAsync(string productId);

        /// <summary>
        /// Get variations for a product (simple list)
        /// </summary>
        Task<List<ProductVariationDto>> GetProductVariationsAsync(string productId);

        /// <summary>
        /// Get variation by ID
        /// </summary>
        Task<ProductVariationDto?> GetVariationByIdAsync(string variationId);

        #endregion

        #region Bundle Operations

        /// <summary>
        /// Get all active bundles
        /// </summary>
        Task<List<BundleDto>> GetAllBundlesAsync();

        /// <summary>
        /// Get bundle by ID with components
        /// </summary>
        Task<BundleDto?> GetBundleByIdAsync(string bundleId);

        /// <summary>
        /// Get all recipes
        /// </summary>
        Task<List<BundleDto>> GetAllRecipesAsync();

        #endregion

        #region Category Operations

        /// <summary>
        /// Get all categories with item counts
        /// </summary>
        Task<List<CategoryDto>> GetAllCategoriesAsync();

        /// <summary>
        /// Get category by ID
        /// </summary>
        Task<CategoryDto?> GetCategoryByIdAsync(string categoryId);

        #endregion

        #region Modifier Operations

        /// <summary>
        /// Get all active modifiers
        /// </summary>
        Task<List<ModifierDto>> GetAllModifiersAsync();

        /// <summary>
        /// Get modifier by ID
        /// </summary>
        Task<ModifierDto?> GetModifierByIdAsync(string modifierId);

        #endregion

        #region Multi-Unit Pricing Operations

        /// <summary>
        /// Get unit pricing options for a product
        /// </summary>
        Task<List<ProductUnitPricingDto>> GetProductUnitPricingAsync(string productId);

        /// <summary>
        /// Get unit pricing options for a variation
        /// </summary>
        Task<List<ProductUnitPricingDto>> GetVariationUnitPricingAsync(string variationId);

        #endregion
    }
}