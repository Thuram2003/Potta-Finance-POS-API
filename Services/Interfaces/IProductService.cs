using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for product-related operations (products, variations, services, unit pricing)
    /// </summary>
    public interface IProductService
    {
        /// <summary>Get all active products (excluding ingredients and assembly components)</summary>
        Task<List<ProductDto>> GetAllProductsAsync();

        /// <summary>Get product by ID with variations, modifiers, and unit pricing</summary>
        Task<ProductDto?> GetProductByIdAsync(string productId);

        /// <summary>Get products by category</summary>
        Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId);

        /// <summary>Get low stock products</summary>
        Task<List<ProductDto>> GetLowStockProductsAsync();

        /// <summary>Get all active services</summary>
        Task<List<ProductDto>> GetAllServicesAsync();

        /// <summary>Get variations for a product with full attribute and value data</summary>
        Task<ProductVariationsWithAttributesDto> GetProductVariationsWithAttributesAsync(string productId);

        /// <summary>Get variations for a product (simple list)</summary>
        Task<List<ProductVariationDto>> GetProductVariationsAsync(string productId);

        /// <summary>Get variation by ID</summary>
        Task<ProductVariationDto?> GetVariationByIdAsync(string variationId);

        /// <summary>Get unit pricing options for a product</summary>
        Task<List<ProductUnitPricingDto>> GetProductUnitPricingAsync(string productId);

        /// <summary>Get unit pricing options for a variation</summary>
        Task<List<ProductUnitPricingDto>> GetVariationUnitPricingAsync(string variationId);

        /// <summary>Get all modifiers associated with a specific product</summary>
        Task<List<ModifierDto>> GetProductModifiersAsync(string productId);
    }
}
