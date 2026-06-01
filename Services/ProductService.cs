using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for product-related operations using Dapper
    /// </summary>
    public class ProductService : IProductService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache? _cache;
        private readonly IModifierService _modifierService;

        private const string CacheKeyAllProducts = "products:all";
        private const string CacheKeyAllServices = "products:services";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public ProductService(
            IConnectionStringProvider connectionStringProvider,
            IMemoryCache? cache = null,
            IModifierService? modifierService = null)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _cache = cache;
            _modifierService = modifierService!;
        }

        public async Task<List<ProductDto>> GetAllProductsAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllProducts, out List<ProductDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Single query: LEFT JOIN filters out assembly components in the DB
            var sql = @"
                SELECT p.productId, p.name, p.sku, p.type, p.description, p.cost, p.salesPrice,
                       p.imagePath, p.inventoryOnHand, p.reorderPoint, p.status, p.taxable,
                       p.taxId, p.createdDate, p.modifiedDate, p.unitOfMeasure, p.categories,
                       (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) as variationCount,
                       CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
                       p.isIngredient, p.costPerUnit, p.purchaseUnit, p.recipeUnit, p.conversionFactor, 
                       p.purchaseMode, p.hasMultiUnitPricing,
                       t.taxName, t.taxType, t.percentage, t.flatRate
                FROM Products p
                LEFT JOIN (
                    SELECT DISTINCT bc.productId
                    FROM BundleComponents bc
                    INNER JOIN BundleItems ba ON bc.bundleId = ba.bundleId
                    WHERE (ba.structure = 'Assembly' OR ba.isRecipe = 1) AND ba.status = 1
                ) asmb ON asmb.productId = p.productId
                LEFT JOIN Taxes t ON p.taxId = t.taxId AND t.isActive = 1
                WHERE p.status = 1 AND asmb.productId IS NULL
                ORDER BY p.name";

            var products = (await connection.QueryAsync<ProductDto>(sql, new { })).ToList();

            // Map categories JSON
            foreach (var product in products)
            {
                product.Categories = ParseCategories(product.Categories);
                product.ImagePath = ConvertPathToUrl(product.ImagePath);
            }

            _cache?.Set(CacheKeyAllProducts, products, CacheDuration);
            return products;
        }

        public async Task<ProductDto?> GetProductByIdAsync(string productId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT p.productId, p.name, p.sku, p.type, p.description, p.cost, p.salesPrice, p.imagePath,
                       p.inventoryOnHand, p.reorderPoint, p.status, p.taxable, p.taxId, p.createdDate, p.modifiedDate,
                       p.unitOfMeasure, p.categories,
                       (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) as variationCount,
                       CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
                       p.isIngredient, p.costPerUnit, p.purchaseUnit, p.recipeUnit, p.conversionFactor, 
                       p.purchaseMode, p.hasMultiUnitPricing,
                       t.taxId as tax_taxId, t.taxName, t.taxType, t.description as tax_description,
                       t.percentage, t.flatRate, t.percentageCap, t.isActive as tax_isActive,
                       t.createdDate as tax_createdDate, t.modifiedDate as tax_modifiedDate
                FROM Products p
                LEFT JOIN Taxes t ON p.taxId = t.taxId AND t.isActive = 1
                WHERE p.productId = @productId AND p.status = 1";

            var product = await connection.QueryFirstOrDefaultAsync<ProductDto>(sql, new { productId });
            if (product == null) return null;

            // Parse categories
            product.Categories = ParseCategories(product.Categories);
            product.ImagePath = ConvertPathToUrl(product.ImagePath);

            // Load variations if product has them
            if (product.HasVariations)
            {
                product.Variations = await GetProductVariationsAsync(productId);
            }

            // Load modifiers for this product
            if (_modifierService != null)
            {
                product.Modifiers = await GetProductModifiersAsync(productId);
            }

            // Load multi-unit pricing for this product
            product.UnitPricing = await GetProductUnitPricingAsync(productId);

            // Populate full Tax object if tax exists
            var taxIdValue = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT taxId FROM Taxes WHERE taxId = @taxId AND isActive = 1",
                new { taxId = product.TaxId });

            if (!string.IsNullOrEmpty(taxIdValue))
            {
                var tax = await connection.QueryFirstOrDefaultAsync<TaxDTO>(
                    @"SELECT taxId, taxName, taxType, description, percentage, flatRate, 
                             percentageCap, isActive, createdDate, modifiedDate
                      FROM Taxes WHERE taxId = @taxId AND isActive = 1",
                    new { taxId = taxIdValue });

                if (tax != null)
                {
                    product.Tax = tax;
                }
            }

            return product;
        }

        public async Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT productId, name, sku, type, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       unitOfMeasure, categories,
                       (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = Products.productId AND status = 1) as variationCount,
                       CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = Products.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
                       isIngredient, costPerUnit, purchaseUnit, recipeUnit, conversionFactor, 
                       purchaseMode, hasMultiUnitPricing
                FROM Products 
                WHERE status = 1 AND (categoryId = @categoryId OR categories LIKE @categoryPattern)
                ORDER BY name";

            var products = (await connection.QueryAsync<ProductDto>(sql, new
            {
                categoryId,
                categoryPattern = $"%{categoryId}%"
            })).ToList();

            foreach (var product in products)
            {
                product.Categories = ParseCategories(product.Categories);
                product.ImagePath = ConvertPathToUrl(product.ImagePath);
            }

            return products;
        }

        public async Task<List<ProductDto>> GetLowStockProductsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT productId, name, sku, type, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       unitOfMeasure, categories,
                       (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = Products.productId AND status = 1) as variationCount,
                       CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = Products.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
                       isIngredient, costPerUnit, purchaseUnit, recipeUnit, conversionFactor, 
                       purchaseMode, hasMultiUnitPricing
                FROM Products 
                WHERE status = 1 AND inventoryOnHand < reorderPoint AND reorderPoint > 0
                ORDER BY (inventoryOnHand / NULLIF(reorderPoint, 0)) ASC";

            var products = (await connection.QueryAsync<ProductDto>(sql)).ToList();

            foreach (var product in products)
            {
                product.Categories = ParseCategories(product.Categories);
                product.ImagePath = ConvertPathToUrl(product.ImagePath);
            }

            return products;
        }

        public async Task<List<ProductDto>> GetAllServicesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllServices, out List<ProductDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT p.productId, p.name, p.sku, p.type, p.description, p.cost, p.salesPrice,
                       p.imagePath, p.inventoryOnHand, p.reorderPoint, p.status, p.taxable,
                       p.taxId, p.createdDate, p.modifiedDate, p.unitOfMeasure, p.categories,
                       p.hasVariations, p.variationCount, p.isIngredient,
                       p.costPerUnit, p.purchaseUnit, p.recipeUnit, p.conversionFactor, p.purchaseMode,
                       p.hasMultiUnitPricing,
                       t.taxName, t.taxType, t.percentage, t.flatRate
                FROM Products p
                LEFT JOIN Taxes t ON p.taxId = t.taxId AND t.isActive = 1
                WHERE p.status = 1 AND p.type = 'Service'
                ORDER BY p.name";

            var services = (await connection.QueryAsync<ProductDto>(sql)).ToList();

            foreach (var service in services)
            {
                service.Categories = ParseCategories(service.Categories);
                service.ImagePath = ConvertPathToUrl(service.ImagePath);
            }

            _cache?.Set(CacheKeyAllServices, services, CacheDuration);
            return services;
        }

        public async Task<ProductVariationsWithAttributesDto> GetProductVariationsWithAttributesAsync(string productId)
        {
            var result = new ProductVariationsWithAttributesDto { ProductId = productId };

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Product name
            result.ProductName = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT name FROM Products WHERE productId = @productId",
                new { productId }) ?? "";

            // All attributes + values for this product's variations
            var attrSql = @"
                SELECT DISTINCT
                    pa.attributeId,
                    pa.attributeName,
                    pav.valueId,
                    pav.valueName
                FROM ProductVariations pv
                INNER JOIN VariationAttributeValues vav ON vav.variationId = pv.variationId
                INNER JOIN ProductAttributes pa ON pa.attributeId = vav.attributeId
                LEFT JOIN ProductAttributeValues pav ON pav.valueId = vav.valueId
                WHERE pv.parentProductId = @productId AND pv.status = 1
                ORDER BY pa.attributeName, pav.valueName";

            var attributeMap = new Dictionary<string, ProductAttributeDto>();
            var attrResults = await connection.QueryAsync(attrSql, new { productId });

            foreach (var row in attrResults)
            {
                var attrId = (string)row.attributeId;
                var attrName = (string)row.attributeName;

                if (!attributeMap.TryGetValue(attrId, out var attr))
                {
                    attr = new ProductAttributeDto { AttributeId = attrId, AttributeName = attrName };
                    attributeMap[attrId] = attr;
                }

                var valueId = row.valueId as string;
                var valueName = row.valueName as string;
                if (!string.IsNullOrEmpty(valueId))
                {
                    if (!attr.Values.Any(v => v.ValueId == valueId))
                    {
                        attr.Values.Add(new ProductAttributeValueDto
                        {
                            ValueId = valueId,
                            ValueName = valueName ?? ""
                        });
                    }
                }
            }

            result.Attributes.AddRange(attributeMap.Values);
            result.Variations = await GetProductVariationsAsync(productId);

            return result;
        }

        public async Task<List<ProductVariationDto>> GetProductVariationsAsync(string productId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT v.variationId, v.parentProductId, v.sku, v.name, v.cost, v.salesPrice,
                       v.inventoryOnHand, v.reorderPoint, v.imagePath, v.status,
                       p.name as parentProductName
                FROM ProductVariations v
                INNER JOIN Products p ON v.parentProductId = p.productId
                WHERE v.parentProductId = @productId AND v.status = 1
                ORDER BY v.name";

            var variations = (await connection.QueryAsync<ProductVariationDto>(sql, new { productId })).ToList();

            foreach (var variation in variations)
            {
                variation.ImagePath = ConvertPathToUrl(variation.ImagePath);
                variation.FullDisplayName = $"{variation.ParentProductName} - {variation.Name}";
            }

            return variations;
        }

        public async Task<ProductVariationDto?> GetVariationByIdAsync(string variationId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT v.variationId, v.parentProductId, v.sku, v.name, v.cost, v.salesPrice,
                       v.inventoryOnHand, v.reorderPoint, v.imagePath, v.status,
                       p.name as parentProductName
                FROM ProductVariations v
                INNER JOIN Products p ON v.parentProductId = p.productId
                WHERE v.variationId = @variationId AND v.status = 1";

            var variation = await connection.QueryFirstOrDefaultAsync<ProductVariationDto>(sql, new { variationId });
            if (variation != null)
            {
                variation.ImagePath = ConvertPathToUrl(variation.ImagePath);
                variation.FullDisplayName = $"{variation.ParentProductName} - {variation.Name}";
            }

            return variation;
        }

        public async Task<List<ProductUnitPricingDto>> GetProductUnitPricingAsync(string productId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get base product price
            var basePrice = await connection.QueryFirstOrDefaultAsync<decimal>(
                "SELECT salesPrice FROM Products WHERE productId = @productId",
                new { productId });

            // Get unit pricing options
            var sql = @"
                SELECT unitPricingId, productId, variationId, packageName, baseUnit, 
                       unitsPerPackage, packagePrice, sku, isActive, 
                       createdDate, modifiedDate
                FROM ProductUnitPricing
                WHERE productId = @productId AND isActive = 1
                ORDER BY unitsPerPackage ASC";

            var unitPricing = (await connection.QueryAsync<ProductUnitPricingDto>(sql, new { productId })).ToList();

            foreach (var pricing in unitPricing)
            {
                pricing.BaseUnitPrice = basePrice;
            }

            return unitPricing;
        }

        public async Task<List<ProductUnitPricingDto>> GetVariationUnitPricingAsync(string variationId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get base variation price
            var basePrice = await connection.QueryFirstOrDefaultAsync<decimal>(
                "SELECT salesPrice FROM ProductVariations WHERE variationId = @variationId",
                new { variationId });

            // Get unit pricing options
            var sql = @"
                SELECT unitPricingId, productId, variationId, packageName, baseUnit, 
                       unitsPerPackage, packagePrice, sku, isActive, 
                       createdDate, modifiedDate
                FROM ProductUnitPricing
                WHERE variationId = @variationId AND isActive = 1
                ORDER BY unitsPerPackage ASC";

            var unitPricing = (await connection.QueryAsync<ProductUnitPricingDto>(sql, new { variationId })).ToList();

            foreach (var pricing in unitPricing)
            {
                pricing.BaseUnitPrice = basePrice;
            }

            return unitPricing;
        }

        public async Task<List<ModifierDto>> GetProductModifiersAsync(string productId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get the availableModifiers field from the product
            var availableModifiersStr = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT availableModifiers FROM Products WHERE productId = @productId AND status = 1",
                new { productId });

            if (string.IsNullOrEmpty(availableModifiersStr))
                return new List<ModifierDto>();

            // Parse the comma-separated modifier IDs
            var modifierIds = availableModifiersStr.Split(',')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            if (modifierIds.Count == 0)
                return new List<ModifierDto>();

            // Build parameterized IN clause
            var placeholders = string.Join(",", modifierIds.Select((_, i) => $"@modId{i}"));
            var parameters = new DynamicParameters();
            for (int i = 0; i < modifierIds.Count; i++)
            {
                parameters.Add($"@modId{i}", modifierIds[i]);
            }

            var sql = $@"
                SELECT m.modifierId, m.modifierName, m.priceChange, m.sortOrder, m.status, 
                       m.createdDate, m.modifiedDate, m.recipeId, m.useRecipePrice,
                       b.name as recipeName, b.cost as recipeCost
                FROM Modifiers m
                LEFT JOIN BundleItems b ON m.recipeId = b.bundleId
                WHERE m.modifierId IN ({placeholders}) AND m.status = 1
                ORDER BY m.sortOrder, m.modifierName";

            return (await connection.QueryAsync<ModifierDto>(sql, parameters)).ToList();
        }

        #region Helper Methods

        private List<string> ParseCategories(object? categoriesObj)
        {
            if (categoriesObj == null) return new List<string>();

            var categoriesJson = categoriesObj.ToString();
            if (string.IsNullOrEmpty(categoriesJson)) return new List<string>();

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(categoriesJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private string ConvertPathToUrl(string? path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            var urlPath = path.Replace("\\", "/");
            if (urlPath.StartsWith("Images/", StringComparison.OrdinalIgnoreCase))
            {
                urlPath = urlPath.Substring(7);
            }

            return $"/images/{urlPath}";
        }

        #endregion
    }
}
