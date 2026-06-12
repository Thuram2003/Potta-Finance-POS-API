using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for item search and statistics operations using Dapper
    /// </summary>
    public class ItemSearchService : IItemSearchService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache? _cache;
        private readonly IProductService _productService;
        private readonly IBundleService _bundleService;

        private const string CacheKeyAllItems = "items:all";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public ItemSearchService(
            IConnectionStringProvider connectionStringProvider,
            IMemoryCache? cache = null,
            IProductService? productService = null,
            IBundleService? bundleService = null)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _cache = cache;
            _productService = productService!;
            _bundleService = bundleService!;
        }

        public void InvalidateCache()
        {
            _cache?.Remove(CacheKeyAllItems);
            _cache?.Remove("products:all");
            _cache?.Remove("products:services");
            _cache?.Remove("bundles:all");
            _cache?.Remove("bundles:recipes");
            _cache?.Remove("categories:all");
            _cache?.Remove("modifiers:all");
        }

        public async Task<List<ItemDto>> GetAllItemsAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllItems, out List<ItemDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                -- Products (excluding ingredients, including those in assemblies)
                SELECT p.productId as Id, p.name, p.sku, p.type, p.description,
                       p.cost, p.salesPrice, p.imagePath, p.status, p.taxable, p.taxId,
                       p.createdDate, p.modifiedDate, p.inventoryOnHand, p.reorderPoint,
                       p.unitOfMeasure, p.categories,
                       (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) as variationCount,
                       CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
                       p.isIngredient, p.costPerUnit, p.purchaseUnit, p.recipeUnit,
                       p.conversionFactor, p.purchaseMode, p.hasMultiUnitPricing,
                       t.taxName, t.taxType, t.percentage, t.flatRate,
                       NULL as parentProductId, NULL as attributeValuesDisplay
                FROM Products p
                LEFT JOIN Taxes t ON p.taxId = t.taxId AND t.isActive = 1
                WHERE p.status = 1 AND p.isIngredient = 0

                UNION ALL

                -- Bundles and Recipes
                SELECT b.bundleId as Id, b.name, b.sku,
                       CASE WHEN b.isRecipe = 1 THEN 'Recipe' ELSE 'Bundle' END as type,
                       b.description, b.cost, b.salesPrice, b.imagePath, b.status,
                       b.taxable, b.taxId, b.createdDate, b.modifiedDate,
                       b.inventoryOnHand, b.reorderPoint,
                       '' as unitOfMeasure, '' as categories,
                       0 as hasVariations, 0 as variationCount,
                       0 as isIngredient, 0 as costPerUnit,
                       '' as purchaseUnit, '' as recipeUnit,
                       1 as conversionFactor, 'Standard' as purchaseMode, 0 as hasMultiUnitPricing,
                       t.taxName, t.taxType, t.percentage, t.flatRate,
                       NULL as parentProductId, NULL as attributeValuesDisplay
                FROM BundleItems b
                LEFT JOIN Taxes t ON b.taxId = t.taxId AND t.isActive = 1
                WHERE b.status = 1

                ORDER BY name";

            var items = (await connection.QueryAsync<ItemDto>(sql)).ToList();

            foreach (var item in items)
            {
                item.Categories = ParseCategories(item.Categories);
                item.ImagePath = ConvertPathToUrl(item.ImagePath);
            }

            _cache?.Set(CacheKeyAllItems, items, CacheDuration);
            return items;
        }

        public async Task<ItemSearchResponseDto> GetAllItemsPaginatedAsync(int page = 1, int pageSize = 50)
        {
            // Validate parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Count query
            var countSql = @"
                SELECT
                  (SELECT COUNT(*)
                   FROM Products p
                   WHERE p.status = 1 AND p.isIngredient = 0)
                +
                  (SELECT COUNT(*)
                   FROM BundleItems b
                   WHERE b.status = 1)
                AS totalCount";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql);

            // Data query with pagination
            var offset = (page - 1) * pageSize;
            var parameters = new DynamicParameters();
            parameters.Add("@pageSize", pageSize);
            parameters.Add("@offset", offset);

            var dataSql = @"
                -- Products (excluding ingredients)
                SELECT p.productId as Id, p.name, p.sku, p.type, p.description,
                       p.cost, p.salesPrice, p.imagePath, p.status, p.taxable, p.taxId,
                       p.createdDate, p.modifiedDate, p.inventoryOnHand, p.reorderPoint,
                       p.unitOfMeasure, p.categories,
                       (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) as variationCount,
                       CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
                       p.isIngredient, p.costPerUnit, p.purchaseUnit, p.recipeUnit,
                       p.conversionFactor, p.purchaseMode, p.hasMultiUnitPricing,
                       t.taxName, t.taxType, t.percentage, t.flatRate,
                       NULL as parentProductId, NULL as attributeValuesDisplay
                FROM Products p
                LEFT JOIN Taxes t ON p.taxId = t.taxId AND t.isActive = 1
                WHERE p.status = 1 AND p.isIngredient = 0

                UNION ALL

                -- Bundles and Recipes
                SELECT b.bundleId as Id, b.name, b.sku,
                       CASE WHEN b.isRecipe = 1 THEN 'Recipe' ELSE 'Bundle' END as type,
                       b.description, b.cost, b.salesPrice, b.imagePath, b.status,
                       b.taxable, b.taxId, b.createdDate, b.modifiedDate,
                       b.inventoryOnHand, b.reorderPoint,
                       '' as unitOfMeasure, '' as categories,
                       0 as hasVariations, 0 as variationCount,
                       0 as isIngredient, 0 as costPerUnit,
                       '' as purchaseUnit, '' as recipeUnit,
                       1 as conversionFactor, 'Standard' as purchaseMode, 0 as hasMultiUnitPricing,
                       t.taxName, t.taxType, t.percentage, t.flatRate,
                       NULL as parentProductId, NULL as attributeValuesDisplay
                FROM BundleItems b
                LEFT JOIN Taxes t ON b.taxId = t.taxId AND t.isActive = 1
                WHERE b.status = 1

                ORDER BY name
                LIMIT @pageSize OFFSET @offset";

            var items = (await connection.QueryAsync<ItemDto>(dataSql, parameters)).ToList();

            foreach (var item in items)
            {
                item.Categories = ParseCategories(item.Categories);
                item.ImagePath = ConvertPathToUrl(item.ImagePath);
            }

            return new ItemSearchResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                ItemType = "",
                Category = ""
            };
        }

        public async Task<ItemDto?> GetItemByIdAsync(string itemId)
        {
            // Try to find as product first
            if (_productService != null)
            {
                var product = await _productService.GetProductByIdAsync(itemId);
                if (product != null) return product;
            }

            // Try to find as bundle
            if (_bundleService != null)
            {
                var bundle = await _bundleService.GetBundleByIdAsync(itemId);
                if (bundle != null) return bundle;
            }

            return null;
        }

        public async Task<ItemSearchResponseDto> SearchItemsAsync(ItemSearchDto searchRequest)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Build separate search conditions with proper table aliases
            string productSearchCondition = "";
            string bundleSearchCondition = "";
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                productSearchCondition = @"AND (
            p.name LIKE @searchTerm OR 
            p.sku LIKE @searchTerm OR 
            p.description LIKE @searchTerm OR
            p.categories LIKE @searchTerm
        )";

                bundleSearchCondition = @"AND (
            b.name LIKE @searchTerm OR 
            b.sku LIKE @searchTerm OR 
            b.description LIKE @searchTerm OR
            b.categories LIKE @searchTerm
        )";

                parameters.Add("@searchTerm", $"%{searchRequest.SearchTerm}%");
            }

            // Count query — separate conditions for each subquery
            var countSql = $@"
        SELECT
          (SELECT COUNT(*)
           FROM Products p
           WHERE p.status = 1 AND p.isIngredient = 0
           {productSearchCondition})
        +
          (SELECT COUNT(*)
           FROM BundleItems b
           WHERE b.status = 1
           {bundleSearchCondition})
        AS totalCount";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            // Data query with pagination
            var offset = (searchRequest.Page - 1) * searchRequest.PageSize;
            parameters.Add("@pageSize", searchRequest.PageSize);
            parameters.Add("@offset", offset);

            var dataSql = $@"
        -- Products (excluding ingredients)
        SELECT p.productId as Id, p.name, p.sku, p.type, p.description,
               p.cost, p.salesPrice, p.imagePath, p.status, p.taxable, p.taxId,
               p.createdDate, p.modifiedDate, p.inventoryOnHand, p.reorderPoint,
               p.unitOfMeasure, p.categories,
               (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) as variationCount,
               CASE WHEN (SELECT COUNT(*) FROM ProductVariations WHERE parentProductId = p.productId AND status = 1) > 0 THEN 1 ELSE 0 END as hasVariations,
               p.isIngredient, p.costPerUnit, p.purchaseUnit, p.recipeUnit,
               p.conversionFactor, p.purchaseMode, p.hasMultiUnitPricing,
               t.taxName, t.taxType, t.percentage, t.flatRate,
               NULL as parentProductId, NULL as attributeValuesDisplay
        FROM Products p
        LEFT JOIN Taxes t ON p.taxId = t.taxId AND t.isActive = 1
        WHERE p.status = 1 AND p.isIngredient = 0
        {productSearchCondition}

        UNION ALL

        -- Bundles and Recipes
        SELECT b.bundleId as Id, b.name, b.sku,
               CASE WHEN b.isRecipe = 1 THEN 'Recipe' ELSE 'Bundle' END as type,
               b.description, b.cost, b.salesPrice, b.imagePath, b.status,
               b.taxable, b.taxId, b.createdDate, b.modifiedDate,
               b.inventoryOnHand, b.reorderPoint,
               '' as unitOfMeasure, '' as categories,
               0 as hasVariations, 0 as variationCount,
               0 as isIngredient, 0 as costPerUnit,
               '' as purchaseUnit, '' as recipeUnit,
               1 as conversionFactor, 'Standard' as purchaseMode, 0 as hasMultiUnitPricing,
               t.taxName, t.taxType, t.percentage, t.flatRate,
               NULL as parentProductId, NULL as attributeValuesDisplay
        FROM BundleItems b
        LEFT JOIN Taxes t ON b.taxId = t.taxId AND t.isActive = 1
        WHERE b.status = 1
        {bundleSearchCondition}

        ORDER BY name
        LIMIT @pageSize OFFSET @offset";

            var items = (await connection.QueryAsync<ItemDto>(dataSql, parameters)).ToList();

            foreach (var item in items)
            {
                item.Categories = ParseCategories(item.Categories);
                item.ImagePath = ConvertPathToUrl(item.ImagePath);
            }

            return new ItemSearchResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = searchRequest.Page,
                PageSize = searchRequest.PageSize,
                ItemType = "",
                Category = ""
            };
        }

        public async Task<ItemStatisticsDto> GetItemStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var stats = new ItemStatisticsDto();

            // Get product statistics
            var productSql = @"
                SELECT 
                    COUNT(*) as TotalProducts,
                    SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END) as ActiveProducts,
                    SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END) as InactiveProducts,
                    SUM(CASE WHEN hasVariations = 1 THEN 1 ELSE 0 END) as ProductsWithVariations,
                    SUM(CASE WHEN inventoryOnHand < reorderPoint AND reorderPoint > 0 THEN 1 ELSE 0 END) as LowStockProducts,
                    SUM(CASE WHEN isIngredient = 1 THEN 1 ELSE 0 END) as IngredientsCount,
                    COALESCE(SUM(inventoryOnHand * cost), 0) as ProductInventoryValue,
                    MAX(createdDate) as LastProductCreated
                FROM Products";

            var productStats = await connection.QueryFirstOrDefaultAsync(productSql);
            if (productStats != null)
            {
                stats.TotalProducts = productStats.TotalProducts;
                stats.ActiveItems += productStats.ActiveProducts;
                stats.InactiveItems += productStats.InactiveProducts;
                stats.ProductsWithVariations = productStats.ProductsWithVariations;
                stats.LowStockItems = productStats.LowStockProducts;
                stats.IngredientsCount = productStats.IngredientsCount;
                stats.TotalInventoryValue = productStats.ProductInventoryValue;
                stats.LastItemCreated = productStats.LastProductCreated;
            }

            // Get bundle statistics
            var bundleSql = @"
                SELECT 
                    COUNT(*) as TotalBundles,
                    SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END) as ActiveBundles,
                    SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END) as InactiveBundles,
                    SUM(CASE WHEN isRecipe = 1 THEN 1 ELSE 0 END) as TotalRecipes,
                    COALESCE(SUM(inventoryOnHand * cost), 0) as BundleInventoryValue,
                    MAX(createdDate) as LastBundleCreated
                FROM BundleItems";

            var bundleStats = await connection.QueryFirstOrDefaultAsync(bundleSql);
            if (bundleStats != null)
            {
                stats.TotalBundles = bundleStats.TotalBundles;
                stats.ActiveItems += bundleStats.ActiveBundles;
                stats.InactiveItems += bundleStats.InactiveBundles;
                stats.TotalRecipes = bundleStats.TotalRecipes;
                stats.TotalInventoryValue += bundleStats.BundleInventoryValue;

                if (bundleStats.LastBundleCreated != null)
                {
                    var lastBundleCreated = (DateTime)bundleStats.LastBundleCreated;
                    if (stats.LastItemCreated == null || lastBundleCreated > stats.LastItemCreated)
                    {
                        stats.LastItemCreated = lastBundleCreated;
                    }
                }
            }

            // Get variation count
            var variationCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ProductVariations WHERE status = 1");
            stats.TotalVariations = variationCount;

            stats.TotalItems = stats.TotalProducts + stats.TotalBundles;

            // Get most popular category
            var categoryName = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT categoryName 
                FROM Categories 
                WHERE isActive = 1 
                ORDER BY categoryName 
                LIMIT 1");
            stats.MostPopularCategory = categoryName ?? "";

            return stats;
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
