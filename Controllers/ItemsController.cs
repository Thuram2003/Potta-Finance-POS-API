using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Controller for item-related operations (products, bundles, recipes, variations)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemsController(IItemService itemService)
        {
            _itemService = itemService;
        }

        #region General Item Operations

        /// <summary>
        /// Get all active items (products, bundles, recipes)
        /// </summary>
        /// <returns>List of active items</returns>
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { })]
        public async Task<ActionResult<ApiResponseDto<List<ItemDto>>>> GetAllItems()
        {
            try
            {
                var items = await _itemService.GetAllItemsAsync();
                return Ok(new ApiResponseDto<List<ItemDto>>
                {
                    Success = true,
                    Message = $"Retrieved {items.Count} items",
                    Data = items
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve items",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get item by ID (works for products, bundles, recipes)
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <returns>Item details if found</returns>
        [HttpGet("{id}")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { "id" })]
        public async Task<ActionResult<ApiResponseDto<ItemDto>>> GetItemById(string id)
        {
            try
            {
                var item = await _itemService.GetItemByIdAsync(id);
                
                if (item == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Item not found",
                        Details = $"No item found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ItemDto>
                {
                    Success = true,
                    Message = "Item retrieved successfully",
                    Data = item
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve item",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Search items (matches Desktop Dashboard search behavior exactly)
        /// 
        /// Search Behavior:
        /// - Searches: name, SKU, description, categories
        /// - Returns: Active items only (status = 1)
        /// - Excludes: Ingredients (isIngredient = 1)
        /// - Excludes: Product variations (returns parent products only)
        /// - Includes: Products, Bundles, and Recipes
        /// - Sorting: Alphabetically by name
        /// 
        /// If no searchTerm provided, returns all active items (excluding ingredients)
        /// </summary>
        /// <param name="searchTerm">Search term to match against name, SKU, description, categories (optional)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Paginated search results</returns>
        [HttpGet("search")]
        public async Task<ActionResult<ApiResponseDto<ItemSearchResponseDto>>> SearchItems(
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var searchRequest = new ItemSearchDto
                {
                    SearchTerm = searchTerm ?? "",
                    Type = "", // Not used - searches all types
                    Category = "", // Not used - searches all categories
                    IncludeInactive = false, // Always false - only active items
                    IncludeVariations = false, // Always false - only parent products
                    Page = page,
                    PageSize = pageSize
                };

                var results = await _itemService.SearchItemsAsync(searchRequest);

                return Ok(new ApiResponseDto<ItemSearchResponseDto>
                {
                    Success = true,
                    Message = $"Found {results.TotalCount} items (showing page {page} of {results.TotalPages})",
                    Data = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to search items",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get item statistics
        /// </summary>
        /// <returns>Item statistics and insights</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponseDto<ItemStatisticsDto>>> GetItemStatistics()
        {
            try
            {
                var statistics = await _itemService.GetItemStatisticsAsync();
                return Ok(new ApiResponseDto<ItemStatisticsDto>
                {
                    Success = true,
                    Message = "Item statistics retrieved successfully",
                    Data = statistics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve item statistics",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Product Operations

        /// <summary>
        /// Get all active products
        /// </summary>
        /// <returns>List of active products</returns>
        [HttpGet("products")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetAllProducts()
        {
            try
            {
                var products = await _itemService.GetAllProductsAsync();
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {products.Count} products",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve products",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get product by ID with variations
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>Product details with variations if found</returns>
        [HttpGet("products/{id}")]
        public async Task<ActionResult<ApiResponseDto<ProductDto>>> GetProductById(string id)
        {
            try
            {
                var product = await _itemService.GetProductByIdAsync(id);
                
                if (product == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Product not found",
                        Details = $"No product found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ProductDto>
                {
                    Success = true,
                    Message = "Product retrieved successfully",
                    Data = product
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve product",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get products by category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>List of products in category</returns>
        [HttpGet("products/category/{categoryId}")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetProductsByCategory(string categoryId)
        {
            try
            {
                var products = await _itemService.GetProductsByCategoryAsync(categoryId);
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {products.Count} products in category",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve products by category",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get low stock products
        /// </summary>
        /// <returns>List of products with low stock</returns>
        [HttpGet("products/low-stock")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetLowStockProducts()
        {
            try
            {
                var products = await _itemService.GetLowStockProductsAsync();
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {products.Count} low stock products",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve low stock products",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Product Variation Operations

        /// <summary>
        /// Get variations for a product with full attribute and value data
        /// 
        /// Returns structured data for building ComboBox UI:
        /// - List of attributes (e.g., Color, Size)
        /// - List of values for each attribute (e.g., Red/Blue/Green for Color)
        /// - List of variations with their attribute-value mappings
        /// 
        /// This allows the client to:
        /// 1. Build ComboBoxes for each attribute
        /// 2. Populate ComboBoxes with available values
        /// 3. Match user selections to specific variations
        /// </summary>
        /// <param name="productId">Parent product ID</param>
        /// <returns>Product variations with attributes and values</returns>
        [HttpGet("products/{productId}/variations")]
        public async Task<ActionResult<ApiResponseDto<ProductVariationsWithAttributesDto>>> GetProductVariations(string productId)
        {
            try
            {
                var result = await _itemService.GetProductVariationsWithAttributesAsync(productId);
                
                return Ok(new ApiResponseDto<ProductVariationsWithAttributesDto>
                {
                    Success = true,
                    Message = $"Retrieved {result.Variations.Count} variations with {result.Attributes.Count} attributes",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve product variations",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get variation by ID
        /// </summary>
        /// <param name="id">Variation ID</param>
        /// <returns>Variation details if found</returns>
        [HttpGet("variations/{id}")]
        public async Task<ActionResult<ApiResponseDto<ProductVariationDto>>> GetVariationById(string id)
        {
            try
            {
                var variation = await _itemService.GetVariationByIdAsync(id);
                
                if (variation == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Variation not found",
                        Details = $"No variation found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ProductVariationDto>
                {
                    Success = true,
                    Message = "Variation retrieved successfully",
                    Data = variation
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve variation",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Bundle Operations

        /// <summary>
        /// Get all active bundles
        /// </summary>
        /// <returns>List of active bundles</returns>
        [HttpGet("bundles")]
        public async Task<ActionResult<ApiResponseDto<List<BundleDto>>>> GetAllBundles()
        {
            try
            {
                var bundles = await _itemService.GetAllBundlesAsync();
                return Ok(new ApiResponseDto<List<BundleDto>>
                {
                    Success = true,
                    Message = $"Retrieved {bundles.Count} bundles",
                    Data = bundles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve bundles",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get bundle by ID with components
        /// </summary>
        /// <param name="id">Bundle ID</param>
        /// <returns>Bundle details with components if found</returns>
        [HttpGet("bundles/{id}")]
        public async Task<ActionResult<ApiResponseDto<BundleDto>>> GetBundleById(string id)
        {
            try
            {
                var bundle = await _itemService.GetBundleByIdAsync(id);
                
                if (bundle == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Bundle not found",
                        Details = $"No bundle found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<BundleDto>
                {
                    Success = true,
                    Message = "Bundle retrieved successfully",
                    Data = bundle
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve bundle",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all recipes
        /// </summary>
        /// <returns>List of recipes</returns>
        [HttpGet("recipes")]
        public async Task<ActionResult<ApiResponseDto<List<BundleDto>>>> GetAllRecipes()
        {
            try
            {
                var recipes = await _itemService.GetAllRecipesAsync();
                return Ok(new ApiResponseDto<List<BundleDto>>
                {
                    Success = true,
                    Message = $"Retrieved {recipes.Count} recipes",
                    Data = recipes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve recipes",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Category Operations

        /// <summary>
        /// Get all categories with item counts
        /// </summary>
        /// <returns>List of categories</returns>
        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponseDto<List<CategoryDto>>>> GetAllCategories()
        {
            try
            {
                var categories = await _itemService.GetAllCategoriesAsync();
                return Ok(new ApiResponseDto<List<CategoryDto>>
                {
                    Success = true,
                    Message = $"Retrieved {categories.Count} categories",
                    Data = categories
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve categories",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get category by ID
        /// </summary>
        /// <param name="id">Category ID</param>
        /// <returns>Category details if found</returns>
        [HttpGet("categories/{id}")]
        public async Task<ActionResult<ApiResponseDto<CategoryDto>>> GetCategoryById(string id)
        {
            try
            {
                var category = await _itemService.GetCategoryByIdAsync(id);
                
                if (category == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Category not found",
                        Details = $"No category found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<CategoryDto>
                {
                    Success = true,
                    Message = "Category retrieved successfully",
                    Data = category
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve category",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Modifier Operations

        /// <summary>
        /// Get all active modifiers
        /// </summary>
        /// <returns>List of active modifiers</returns>
        [HttpGet("modifiers")]
        public async Task<ActionResult<ApiResponseDto<List<ModifierDto>>>> GetAllModifiers()
        {
            try
            {
                var modifiers = await _itemService.GetAllModifiersAsync();
                return Ok(new ApiResponseDto<List<ModifierDto>>
                {
                    Success = true,
                    Message = $"Retrieved {modifiers.Count} modifiers",
                    Data = modifiers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve modifiers",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get modifier by ID
        /// </summary>
        /// <param name="id">Modifier ID</param>
        /// <returns>Modifier details if found</returns>
        [HttpGet("modifiers/{id}")]
        public async Task<ActionResult<ApiResponseDto<ModifierDto>>> GetModifierById(string id)
        {
            try
            {
                var modifier = await _itemService.GetModifierByIdAsync(id);
                
                if (modifier == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Modifier not found",
                        Details = $"No modifier found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ModifierDto>
                {
                    Success = true,
                    Message = "Modifier retrieved successfully",
                    Data = modifier
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve modifier",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Multi-Unit Pricing Operations

        /// <summary>
        /// Get unit pricing options for a product
        /// 
        /// Returns different package sizes/units for selling the same product.
        /// Example: Sell Coca-Cola by bottle (330ml) or by crate (24 bottles)
        /// 
        /// Each option includes:
        /// - Package name (e.g., "Bottle", "Crate")
        /// - Units per package (e.g., 1 bottle, 24 bottles)
        /// - Package price
        /// - Price per unit in package (calculated)
        /// - Discount percentage compared to base unit price
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>List of unit pricing options</returns>
        [HttpGet("products/{productId}/unit-pricing")]
        public async Task<ActionResult<ApiResponseDto<List<ProductUnitPricingDto>>>> GetProductUnitPricing(string productId)
        {
            try
            {
                var unitPricing = await _itemService.GetProductUnitPricingAsync(productId);
                return Ok(new ApiResponseDto<List<ProductUnitPricingDto>>
                {
                    Success = true,
                    Message = $"Retrieved {unitPricing.Count} unit pricing options",
                    Data = unitPricing
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve unit pricing",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get unit pricing options for a product variation
        /// 
        /// Same as product unit pricing but for specific variations.
        /// Example: Coca-Cola (Regular) might have different pricing than Coca-Cola (Diet)
        /// </summary>
        /// <param name="variationId">Variation ID</param>
        /// <returns>List of unit pricing options</returns>
        [HttpGet("variations/{variationId}/unit-pricing")]
        public async Task<ActionResult<ApiResponseDto<List<ProductUnitPricingDto>>>> GetVariationUnitPricing(string variationId)
        {
            try
            {
                var unitPricing = await _itemService.GetVariationUnitPricingAsync(variationId);
                return Ok(new ApiResponseDto<List<ProductUnitPricingDto>>
                {
                    Success = true,
                    Message = $"Retrieved {unitPricing.Count} unit pricing options",
                    Data = unitPricing
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve unit pricing",
                    Details = ex.Message
                });
            }
        }

        #endregion
    }
}