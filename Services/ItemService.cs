using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using PottaAPI.Models;
using System.Data;
using Newtonsoft.Json;

namespace PottaAPI.Services
{
    // Item service for products, bundles, recipes, variations
    public class ItemService : IItemService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache? _cache;

        // Cache keys
        private const string CacheKeyAllProducts = "items:all_products";
        private const string CacheKeyAllBundles  = "items:all_bundles";
        private const string CacheKeyAllRecipes  = "items:all_recipes";
        private const string CacheKeyAllItems    = "items:all_items";
        private const string CacheKeyAssemblyIds = "items:assembly_ids";
        private const string CacheKeyAllModifiers = "items:all_modifiers";
        private const string CacheKeyAllCategories = "items:all_categories";

        // How long list results stay cached (products change infrequently during a shift)
        private static readonly TimeSpan ListCacheDuration = TimeSpan.FromMinutes(2);

        public ItemService(string connectionString, IMemoryCache? cache = null)
        {
            _connectionString = connectionString;
            _cache = cache;
        }

        /// <summary>Invalidates all item-related cache entries (call after any write operation).</summary>
        public void InvalidateCache()
        {
            _cache?.Remove(CacheKeyAllProducts);
            _cache?.Remove(CacheKeyAllBundles);
            _cache?.Remove(CacheKeyAllRecipes);
            _cache?.Remove(CacheKeyAllItems);
            _cache?.Remove(CacheKeyAssemblyIds);
            _cache?.Remove(CacheKeyAllModifiers);
            _cache?.Remove(CacheKeyAllCategories);
        }

        #region General Item Operations

        public async Task<List<ItemDto>> GetAllItemsAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllItems, out List<ItemDto>? cached) && cached != null)
                return cached;

            // Run both queries in parallel — they use independent connections
            var productsTask = GetAllProductsAsync();
            var bundlesTask  = GetAllBundlesAsync();
            await Task.WhenAll(productsTask, bundlesTask);

            var items = new List<ItemDto>(productsTask.Result.Count + bundlesTask.Result.Count);
            items.AddRange(productsTask.Result.Cast<ItemDto>());
            items.AddRange(bundlesTask.Result.Cast<ItemDto>());
            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            _cache?.Set(CacheKeyAllItems, items, ListCacheDuration);
            return items;
        }

        public async Task<ItemDto?> GetItemByIdAsync(string itemId)
        {
            // Try to find as product first
            var product = await GetProductByIdAsync(itemId);
            if (product != null) return product;

            // Try to find as bundle
            var bundle = await GetBundleByIdAsync(itemId);
            if (bundle != null) return bundle;

            return null;
        }

        public async Task<ItemSearchResponseDto> SearchItemsAsync(ItemSearchDto searchRequest)
        {
            var items = new List<ItemDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Build search condition fragments (no WHERE keyword — appended with AND below)
            string productSearchCondition;
            string bundleSearchCondition;
            var parameters = new List<SqliteParameter>();

            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                productSearchCondition = @"AND (
                    p.name LIKE @searchTerm OR 
                    p.sku  LIKE @searchTerm OR 
                    p.description LIKE @searchTerm OR
                    p.categories  LIKE @searchTerm
                )";

                bundleSearchCondition = @"AND (
                    b.name LIKE @searchTerm OR 
                    b.sku  LIKE @searchTerm OR 
                    b.description LIKE @searchTerm
                )";

                parameters.Add(new SqliteParameter("@searchTerm", $"%{searchRequest.SearchTerm}%"));
            }
            else
            {
                productSearchCondition = "";
                bundleSearchCondition  = "";
            }

            // ── Count query ────────────────────────────────────────────────────────────
            var countCmd = connection.CreateCommand();
            countCmd.CommandTimeout = 30;
            countCmd.CommandText = $@"
                SELECT
                  (SELECT COUNT(*)
                   FROM Products p
                   LEFT JOIN (
                       SELECT DISTINCT bc.productId
                       FROM BundleComponents bc
                       INNER JOIN BundleItems ba ON bc.bundleId = ba.bundleId
                       WHERE (ba.structure = 'Assembly' OR ba.isRecipe = 1) AND ba.status = 1
                   ) asmb ON asmb.productId = p.productId
                   WHERE p.status = 1 AND p.isIngredient = 0 AND asmb.productId IS NULL
                   {productSearchCondition})
                +
                  (SELECT COUNT(*)
                   FROM BundleItems b
                   WHERE b.status = 1
                   {bundleSearchCondition})
                AS totalCount";

            foreach (var p in parameters)
                countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

            // ── Data query: UNION ALL with DB-level LIMIT / OFFSET ─────────────────────
            var offset = (searchRequest.Page - 1) * searchRequest.PageSize;

            var dataCmd = connection.CreateCommand();
            dataCmd.CommandTimeout = 30;
            dataCmd.CommandText = $@"
                SELECT p.productId as Id, p.name, p.sku, 'Product' as type, p.description,
                       p.cost, p.salesPrice, p.imagePath, p.status, p.taxable, p.taxId,
                       p.createdDate, p.modifiedDate, p.inventoryOnHand, p.reorderPoint,
                       p.unitOfMeasure, p.categories, p.hasVariations, p.variationCount,
                       p.isIngredient, p.costPerUnit, p.purchaseUnit, p.recipeUnit,
                       p.conversionFactor, p.purchaseMode
                FROM Products p
                LEFT JOIN (
                    SELECT DISTINCT bc.productId
                    FROM BundleComponents bc
                    INNER JOIN BundleItems ba ON bc.bundleId = ba.bundleId
                    WHERE (ba.structure = 'Assembly' OR ba.isRecipe = 1) AND ba.status = 1
                ) asmb ON asmb.productId = p.productId
                WHERE p.status = 1 AND p.isIngredient = 0 AND asmb.productId IS NULL
                {productSearchCondition}

                UNION ALL

                SELECT b.bundleId as Id, b.name, b.sku,
                       CASE WHEN b.isRecipe = 1 THEN 'Recipe' ELSE 'Bundle' END as type,
                       b.description, b.cost, b.salesPrice, b.imagePath, b.status,
                       b.taxable, b.taxId, b.createdDate, b.modifiedDate,
                       b.inventoryOnHand, b.reorderPoint,
                       '' as unitOfMeasure, '' as categories,
                       0 as hasVariations, 0 as variationCount,
                       0 as isIngredient, 0 as costPerUnit,
                       '' as purchaseUnit, '' as recipeUnit,
                       1 as conversionFactor, 'Standard' as purchaseMode
                FROM BundleItems b
                WHERE b.status = 1
                {bundleSearchCondition}

                ORDER BY name
                LIMIT @pageSize OFFSET @offset";

            foreach (var p in parameters)
                dataCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
            dataCmd.Parameters.AddWithValue("@pageSize", searchRequest.PageSize);
            dataCmd.Parameters.AddWithValue("@offset", offset);

            using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var typeStr = reader["type"]?.ToString() ?? "Product";
                var item = typeStr is "Bundle" or "Recipe"
                    ? CreateBundleFromDataReader(reader)
                    : (ItemDto?)CreateProductFromDataReader(reader);
                if (item != null) items.Add(item);
            }

            return new ItemSearchResponseDto
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = searchRequest.Page,
                PageSize   = searchRequest.PageSize,
                ItemType   = "",
                Category   = ""
            };
        }

        private async Task ExecuteSearchQuery(SqliteConnection connection, string query, List<SqliteParameter> parameters, List<ItemDto> items, string itemType)
        {
            // Kept for potential future use; SearchItemsAsync now uses a single UNION ALL query.
            var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = 30;
            
            foreach (var param in parameters)
            {
                command.Parameters.Add(new SqliteParameter(param.ParameterName, param.Value));
            }

            using var reader = await command.ExecuteReaderAsync();
            int count = 0;
            while (await reader.ReadAsync())
            {
                var item = CreateItemFromDataReader(reader, itemType);
                if (item != null)
                {
                    items.Add(item);
                    count++;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"ExecuteSearchQuery: Found {count} items of type '{itemType}'");
        }

        public async Task<ItemStatisticsDto> GetItemStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var stats = new ItemStatisticsDto();

            // Get product statistics
            var productCommand = connection.CreateCommand();
            productCommand.CommandText = @"
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

            using var productReader = await productCommand.ExecuteReaderAsync();
            if (await productReader.ReadAsync())
            {
                stats.TotalProducts = productReader.GetInt32("TotalProducts");
                stats.ActiveItems += productReader.GetInt32("ActiveProducts");
                stats.InactiveItems += productReader.GetInt32("InactiveProducts");
                stats.ProductsWithVariations = productReader.GetInt32("ProductsWithVariations");
                stats.LowStockItems = productReader.GetInt32("LowStockProducts");
                stats.IngredientsCount = productReader.GetInt32("IngredientsCount");
                stats.TotalInventoryValue = productReader.GetDecimal("ProductInventoryValue");
                
                if (!productReader.IsDBNull("LastProductCreated"))
                {
                    stats.LastItemCreated = productReader.GetDateTime("LastProductCreated");
                }
            }

            // Get bundle statistics
            var bundleCommand = connection.CreateCommand();
            bundleCommand.CommandText = @"
                SELECT 
                    COUNT(*) as TotalBundles,
                    SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END) as ActiveBundles,
                    SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END) as InactiveBundles,
                    SUM(CASE WHEN isRecipe = 1 THEN 1 ELSE 0 END) as TotalRecipes,
                    COALESCE(SUM(inventoryOnHand * cost), 0) as BundleInventoryValue,
                    MAX(createdDate) as LastBundleCreated
                FROM BundleItems";

            using var bundleReader = await bundleCommand.ExecuteReaderAsync();
            if (await bundleReader.ReadAsync())
            {
                stats.TotalBundles = bundleReader.GetInt32("TotalBundles");
                stats.ActiveItems += bundleReader.GetInt32("ActiveBundles");
                stats.InactiveItems += bundleReader.GetInt32("InactiveBundles");
                stats.TotalRecipes = bundleReader.GetInt32("TotalRecipes");
                stats.TotalInventoryValue += bundleReader.GetDecimal("BundleInventoryValue");
                
                if (!bundleReader.IsDBNull("LastBundleCreated"))
                {
                    var lastBundleCreated = bundleReader.GetDateTime("LastBundleCreated");
                    if (stats.LastItemCreated == null || lastBundleCreated > stats.LastItemCreated)
                    {
                        stats.LastItemCreated = lastBundleCreated;
                    }
                }
            }

            // Get variation count
            var variationCommand = connection.CreateCommand();
            variationCommand.CommandText = "SELECT COUNT(*) FROM ProductVariations WHERE status = 1";
            stats.TotalVariations = Convert.ToInt32(await variationCommand.ExecuteScalarAsync());

            stats.TotalItems = stats.TotalProducts + stats.TotalBundles;

            // Get most popular category (simplified - just get the first category with items)
            var categoryCommand = connection.CreateCommand();
            categoryCommand.CommandText = @"
                SELECT categoryName as name 
                FROM Categories 
                WHERE isActive = 1 
                ORDER BY categoryName 
                LIMIT 1";
            
            var categoryResult = await categoryCommand.ExecuteScalarAsync();
            stats.MostPopularCategory = categoryResult?.ToString() ?? "";

            return stats;
        }

        #endregion

        #region Product Operations

        public async Task<List<ProductDto>> GetAllProductsAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllProducts, out List<ProductDto>? cached) && cached != null)
                return cached;

            var products = new List<ProductDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Single query: LEFT JOIN filters out assembly components in the DB,
            // eliminating the separate GetProductsUsedInAssemblyBundlesAsync() call.
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.productId, p.name, p.sku, p.type, p.description, p.cost, p.salesPrice,
                       p.imagePath, p.inventoryOnHand, p.reorderPoint, p.status, p.taxable,
                       p.taxId, p.createdDate, p.modifiedDate, p.unitOfMeasure, p.categories,
                       p.hasVariations, p.variationCount, p.isIngredient,
                       p.costPerUnit, p.purchaseUnit, p.recipeUnit, p.conversionFactor, p.purchaseMode
                FROM Products p
                LEFT JOIN (
                    SELECT DISTINCT bc.productId
                    FROM BundleComponents bc
                    INNER JOIN BundleItems ba ON bc.bundleId = ba.bundleId
                    WHERE (ba.structure = 'Assembly' OR ba.isRecipe = 1) AND ba.status = 1
                ) asmb ON asmb.productId = p.productId
                WHERE p.status = 1 AND asmb.productId IS NULL
                ORDER BY p.name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var product = CreateProductFromDataReader(reader);
                if (product != null) products.Add(product);
            }

            _cache?.Set(CacheKeyAllProducts, products, ListCacheDuration);
            return products;
        }

        public async Task<ProductDto?> GetProductByIdAsync(string productId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT productId, name, sku, type, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       unitOfMeasure, categories, hasVariations, variationCount, isIngredient,
                       costPerUnit, purchaseUnit, recipeUnit, conversionFactor, purchaseMode
                FROM Products 
                WHERE productId = @productId AND status = 1";
            command.Parameters.AddWithValue("@productId", productId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var product = CreateProductFromDataReader(reader);
                if (product != null)
                {
                    // Load variations if product has them
                    if (product.HasVariations)
                    {
                        product.Variations = await GetProductVariationsAsync(productId);
                    }
                    
                    // Load modifiers for this product
                    product.Modifiers = await GetProductModifiersAsync(productId);
                    
                    // Load multi-unit pricing for this product
                    product.UnitPricing = await GetProductUnitPricingAsync(productId);
                }
                return product;
            }

            return null;
        }

        public async Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId)
        {
            var products = new List<ProductDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT productId, name, sku, type, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       unitOfMeasure, categories, hasVariations, variationCount, isIngredient,
                       costPerUnit, purchaseUnit, recipeUnit, conversionFactor, purchaseMode
                FROM Products 
                WHERE status = 1 AND (categoryId = @categoryId OR categories LIKE @categoryPattern)
                ORDER BY name";
            command.Parameters.AddWithValue("@categoryId", categoryId);
            command.Parameters.AddWithValue("@categoryPattern", $"%{categoryId}%");

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var product = CreateProductFromDataReader(reader);
                if (product != null)
                {
                    products.Add(product);
                }
            }

            return products;
        }

        public async Task<List<ProductDto>> GetLowStockProductsAsync()
        {
            var products = new List<ProductDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT productId, name, sku, type, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       unitOfMeasure, categories, hasVariations, variationCount, isIngredient,
                       costPerUnit, purchaseUnit, recipeUnit, conversionFactor, purchaseMode
                FROM Products 
                WHERE status = 1 AND inventoryOnHand < reorderPoint AND reorderPoint > 0
                ORDER BY (inventoryOnHand / NULLIF(reorderPoint, 0)) ASC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var product = CreateProductFromDataReader(reader);
                if (product != null)
                {
                    products.Add(product);
                }
            }

            return products;
        }

        #endregion

        #region Product Variation Operations

        public async Task<ProductVariationsWithAttributesDto> GetProductVariationsWithAttributesAsync(string productId)
        {
            var result = new ProductVariationsWithAttributesDto { ProductId = productId };

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Product name
            var productCommand = connection.CreateCommand();
            productCommand.CommandText = "SELECT name FROM Products WHERE productId = @productId";
            productCommand.Parameters.AddWithValue("@productId", productId);
            result.ProductName = (await productCommand.ExecuteScalarAsync())?.ToString() ?? "";

            // All attributes + values for this product's variations in ONE query.
            // ProductAttributes has NO productId column — the link is:
            //   ProductVariations (parentProductId) → VariationAttributeValues → ProductAttributes → ProductAttributeValues
            var attrCmd = connection.CreateCommand();
            attrCmd.CommandText = @"
                SELECT DISTINCT
                    pa.attributeId,
                    pa.attributeName,
                    pav.valueId,
                    pav.valueName
                FROM ProductVariations pv
                INNER JOIN VariationAttributeValues vav ON vav.variationId = pv.variationId
                INNER JOIN ProductAttributes pa         ON pa.attributeId  = vav.attributeId
                LEFT  JOIN ProductAttributeValues pav   ON pav.valueId     = vav.valueId
                WHERE pv.parentProductId = @productId
                  AND pv.status = 1
                ORDER BY pa.attributeName, pav.valueName";
            attrCmd.Parameters.AddWithValue("@productId", productId);

            var attributeMap = new Dictionary<string, ProductAttributeDto>();
            using (var attrReader = await attrCmd.ExecuteReaderAsync())
            {
                while (await attrReader.ReadAsync())
                {
                    var attrId   = attrReader["attributeId"]?.ToString() ?? "";
                    var attrName = attrReader["attributeName"]?.ToString() ?? "";

                    if (!attributeMap.TryGetValue(attrId, out var attr))
                    {
                        attr = new ProductAttributeDto { AttributeId = attrId, AttributeName = attrName };
                        attributeMap[attrId] = attr;
                    }

                    var valueId   = attrReader["valueId"]?.ToString();
                    var valueName = attrReader["valueName"]?.ToString();
                    if (!string.IsNullOrEmpty(valueId))
                    {
                        // Avoid duplicates (DISTINCT on the SQL side handles most cases)
                        if (!attr.Values.Any(v => v.ValueId == valueId))
                        {
                            attr.Values.Add(new ProductAttributeValueDto
                            {
                                ValueId   = valueId,
                                ValueName = valueName ?? ""
                            });
                        }
                    }
                }
            }
            result.Attributes.AddRange(attributeMap.Values);

            // Variations (reuses existing method)
            result.Variations = await GetProductVariationsAsync(productId);

            return result;
        }

        public async Task<List<ProductVariationDto>> GetProductVariationsAsync(string productId)
        {
            var variations = new List<ProductVariationDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT v.variationId, v.parentProductId, v.sku, v.name, v.cost, v.salesPrice,
                       v.inventoryOnHand, v.reorderPoint, v.imagePath, v.status,
                       p.name as parentProductName
                FROM ProductVariations v
                INNER JOIN Products p ON v.parentProductId = p.productId
                WHERE v.parentProductId = @productId AND v.status = 1
                ORDER BY v.name";
            command.Parameters.AddWithValue("@productId", productId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var variation = CreateVariationFromDataReader(reader);
                if (variation != null)
                {
                    variations.Add(variation);
                }
            }

            return variations;
        }

        public async Task<ProductVariationDto?> GetVariationByIdAsync(string variationId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT v.variationId, v.parentProductId, v.sku, v.name, v.cost, v.salesPrice,
                       v.inventoryOnHand, v.reorderPoint, v.imagePath, v.status,
                       p.name as parentProductName
                FROM ProductVariations v
                INNER JOIN Products p ON v.parentProductId = p.productId
                WHERE v.variationId = @variationId AND v.status = 1";
            command.Parameters.AddWithValue("@variationId", variationId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return CreateVariationFromDataReader(reader);
            }

            return null;
        }

        #endregion

        #region Bundle Operations

        public async Task<List<BundleDto>> GetAllBundlesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllBundles, out List<BundleDto>? cached) && cached != null)
                return cached;

            var bundles = new List<BundleDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT bundleId, name, sku, structure, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       isRecipe, servingSize, preparationTime, cookingInstructions
                FROM BundleItems 
                WHERE status = 1 AND isRecipe = 0
                ORDER BY name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bundle = CreateBundleFromDataReader(reader);
                if (bundle != null) bundles.Add(bundle);
            }

            _cache?.Set(CacheKeyAllBundles, bundles, ListCacheDuration);
            return bundles;
        }

        public async Task<BundleDto?> GetBundleByIdAsync(string bundleId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT bundleId, name, sku, structure, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       isRecipe, servingSize, preparationTime, cookingInstructions
                FROM BundleItems 
                WHERE bundleId = @bundleId AND status = 1";
            command.Parameters.AddWithValue("@bundleId", bundleId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var bundle = CreateBundleFromDataReader(reader);
                if (bundle != null)
                {
                    bundle.Components = await GetBundleComponentsAsync(bundleId);
                }
                return bundle;
            }

            return null;
        }

        public async Task<List<BundleDto>> GetAllRecipesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllRecipes, out List<BundleDto>? cached) && cached != null)
                return cached;

            var recipes = new List<BundleDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT bundleId, name, sku, structure, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       isRecipe, servingSize, preparationTime, cookingInstructions
                FROM BundleItems 
                WHERE status = 1 AND isRecipe = 1
                ORDER BY name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var recipe = CreateBundleFromDataReader(reader);
                if (recipe != null) recipes.Add(recipe);
            }

            _cache?.Set(CacheKeyAllRecipes, recipes, ListCacheDuration);
            return recipes;
        }

        private async Task<List<BundleComponentDto>> GetBundleComponentsAsync(string bundleId)
        {
            var components = new List<BundleComponentDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT bc.productId, bc.quantity, bc.recipeUnit, p.name as productName, p.salesPrice
                FROM BundleComponents bc
                INNER JOIN Products p ON bc.productId = p.productId
                WHERE bc.bundleId = @bundleId
                ORDER BY p.name";
            command.Parameters.AddWithValue("@bundleId", bundleId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                components.Add(new BundleComponentDto
                {
                    ProductId = reader["productId"]?.ToString() ?? "",
                    ProductName = reader["productName"]?.ToString() ?? "",
                    Quantity = reader.IsDBNull("quantity") ? 0 : reader.GetDecimal("quantity"),
                    RecipeUnit = reader["recipeUnit"]?.ToString() ?? "",
                    ProductPrice = reader.IsDBNull("salesPrice") ? 0 : reader.GetDecimal("salesPrice")
                });
            }

            return components;
        }

        #endregion

        #region Category Operations

        public async Task<List<CategoryDto>> GetAllCategoriesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllCategories, out List<CategoryDto>? cached) && cached != null)
                return cached;

            var categories = new List<CategoryDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.categoryId, c.categoryName as name, c.description, c.isActive, c.createdDate,
                       (SELECT COUNT(*) FROM Products p WHERE p.categoryId = c.categoryId OR p.categories LIKE '%' || c.categoryId || '%') as itemCount
                FROM Categories c
                WHERE c.isActive = 1
                ORDER BY c.categoryName";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new CategoryDto
                {
                    CategoryId = reader["categoryId"]?.ToString() ?? "",
                    Name = reader["name"]?.ToString() ?? "",
                    Description = reader["description"]?.ToString() ?? "",
                    IsActive = reader.GetBoolean("isActive"),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ItemCount = reader.GetInt32("itemCount")
                });
            }

            _cache?.Set(CacheKeyAllCategories, categories, ListCacheDuration);
            return categories;
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(string categoryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.categoryId, c.categoryName as name, c.description, c.isActive, c.createdDate,
                       (SELECT COUNT(*) FROM Products p WHERE p.categoryId = c.categoryId OR p.categories LIKE '%' || c.categoryId || '%') as itemCount
                FROM Categories c
                WHERE c.categoryId = @categoryId AND c.isActive = 1";
            command.Parameters.AddWithValue("@categoryId", categoryId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CategoryDto
                {
                    CategoryId = reader["categoryId"]?.ToString() ?? "",
                    Name = reader["name"]?.ToString() ?? "",
                    Description = reader["description"]?.ToString() ?? "",
                    IsActive = reader.GetBoolean("isActive"),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ItemCount = reader.GetInt32("itemCount")
                };
            }

            return null;
        }

        #endregion

        #region Helper Methods

        // Helper method to get products used in assembly bundles and recipes
        private async Task<HashSet<string>> GetProductsUsedInAssemblyBundlesAsync()
        {
            var assemblyComponentIds = new HashSet<string>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT DISTINCT bc.productId
                FROM BundleComponents bc
                INNER JOIN BundleItems b ON bc.bundleId = b.bundleId
                WHERE (b.structure = 'Assembly' OR b.isRecipe = 1) AND b.status = 1";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                assemblyComponentIds.Add(reader.GetString(0));
            }
            
            return assemblyComponentIds;
        }

        // Safe data reader helper methods
        private bool SafeGetBoolean(SqliteDataReader reader, string columnName, bool defaultValue = false)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetBoolean(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private int SafeGetInt32(SqliteDataReader reader, string columnName, int defaultValue = 0)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private decimal SafeGetDecimal(SqliteDataReader reader, string columnName, decimal defaultValue = 0)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetDecimal(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        private string SafeGetString(SqliteDataReader reader, string columnName, string defaultValue = "")
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts Windows file paths to API-accessible URLs
        /// </summary>
        private string ConvertPathToUrl(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            
            // Replace Windows backslashes with forward slashes
            var urlPath = path.Replace("\\", "/");
            
            // Remove "Images/" prefix if present (case-insensitive)
            if (urlPath.StartsWith("Images/", StringComparison.OrdinalIgnoreCase))
            {
                urlPath = urlPath.Substring(7); // Remove "Images/" (7 characters)
            }
            
            // Return as API path (will be served from /images endpoint)
            return $"/images/{urlPath}";
        }

        private ItemDto? CreateItemFromDataReader(SqliteDataReader reader, string itemType)
        {
            try
            {
                var result = itemType switch
                {
                    "Product" => CreateProductFromDataReader(reader),
                    "Bundle" => CreateBundleFromDataReader(reader),
                    "Variation" => CreateVariationItemFromDataReader(reader),
                    _ => null
                };
                
                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine($"CreateItemFromDataReader: Failed to create item of type '{itemType}'");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateItemFromDataReader ERROR for type '{itemType}': {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private ProductDto? CreateProductFromDataReader(SqliteDataReader reader)
        {
            try
            {
                var categoriesJson = reader["categories"]?.ToString();
                var categories = new List<string>();
                
                if (!string.IsNullOrEmpty(categoriesJson))
                {
                    try
                    {
                        categories = JsonConvert.DeserializeObject<List<string>>(categoriesJson) ?? new List<string>();
                    }
                    catch
                    {
                        categories = new List<string>();
                    }
                }

                // Try to read Id column first (used in search queries), fallback to productId
                string productId = "";
                try
                {
                    productId = reader["Id"]?.ToString() ?? "";
                }
                catch
                {
                    productId = reader["productId"]?.ToString() ?? "";
                }

                return new ProductDto
                {
                    Id = productId,
                    Name = reader["name"]?.ToString() ?? "",
                    SKU = reader["sku"]?.ToString() ?? "",
                    Type = "Product",
                    Description = reader["description"]?.ToString() ?? "",
                    Cost = reader.IsDBNull("cost") ? 0 : reader.GetDecimal("cost"),
                    SalesPrice = reader.IsDBNull("salesPrice") ? 0 : reader.GetDecimal("salesPrice"),
                    ImagePath = ConvertPathToUrl(reader["imagePath"]?.ToString() ?? ""),
                    Status = reader.GetBoolean("status"),
                    Taxable = reader.GetBoolean("taxable"),
                    TaxId = reader["taxId"]?.ToString() ?? "",
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    Categories = categories,
                    InventoryOnHand = reader.IsDBNull("inventoryOnHand") ? 0 : reader.GetDecimal("inventoryOnHand"),
                    ReorderPoint = reader.IsDBNull("reorderPoint") ? 0 : reader.GetDecimal("reorderPoint"),
                    UnitOfMeasure = reader["unitOfMeasure"]?.ToString() ?? "",
                    HasVariations = SafeGetBoolean(reader, "hasVariations"),
                    VariationCount = SafeGetInt32(reader, "variationCount"),
                    IsIngredient = SafeGetBoolean(reader, "isIngredient"),
                    CostPerUnit = SafeGetDecimal(reader, "costPerUnit"),
                    PurchaseUnit = SafeGetString(reader, "purchaseUnit"),
                    RecipeUnit = SafeGetString(reader, "recipeUnit"),
                    ConversionFactor = SafeGetDecimal(reader, "conversionFactor", 1),
                    PurchaseMode = SafeGetString(reader, "purchaseMode", "Standard")
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateProductFromDataReader ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private BundleDto? CreateBundleFromDataReader(SqliteDataReader reader)
        {
            try
            {
                // Try to read Id column first (used in search queries), fallback to bundleId
                string bundleId = "";
                try
                {
                    bundleId = SafeGetString(reader, "Id");
                }
                catch
                {
                    bundleId = SafeGetString(reader, "bundleId");
                }

                return new BundleDto
                {
                    Id = bundleId,
                    Name = SafeGetString(reader, "name"),
                    SKU = SafeGetString(reader, "sku"),
                    Type = SafeGetBoolean(reader, "isRecipe") ? "Recipe" : "Bundle",
                    Description = SafeGetString(reader, "description"),
                    Cost = SafeGetDecimal(reader, "cost"),
                    SalesPrice = SafeGetDecimal(reader, "salesPrice"),
                    ImagePath = ConvertPathToUrl(SafeGetString(reader, "imagePath")),
                    Status = SafeGetBoolean(reader, "status", true),
                    Taxable = SafeGetBoolean(reader, "taxable"),
                    TaxId = SafeGetString(reader, "taxId"),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    Categories = new List<string>(), // Bundles don't have categories in this simplified version
                    Structure = SafeGetString(reader, "structure", "Assembly"),
                    InventoryOnHand = SafeGetDecimal(reader, "inventoryOnHand"),
                    ReorderPoint = SafeGetDecimal(reader, "reorderPoint"),
                    IsRecipe = SafeGetBoolean(reader, "isRecipe"),
                    ServingSize = SafeGetInt32(reader, "servingSize", 1),
                    PreparationTime = SafeGetInt32(reader, "preparationTime"),
                    CookingInstructions = SafeGetString(reader, "cookingInstructions")
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateBundleFromDataReader ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private ProductVariationDto? CreateVariationFromDataReader(SqliteDataReader reader)
        {
            try
            {
                return new ProductVariationDto
                {
                    VariationId = SafeGetString(reader, "variationId"),
                    ParentProductId = SafeGetString(reader, "parentProductId"),
                    SKU = SafeGetString(reader, "sku"),
                    Name = SafeGetString(reader, "name"),
                    Cost = SafeGetDecimal(reader, "cost"),
                    SalesPrice = SafeGetDecimal(reader, "salesPrice"),
                    InventoryOnHand = SafeGetDecimal(reader, "inventoryOnHand"),
                    ReorderPoint = SafeGetDecimal(reader, "reorderPoint"),
                    ImagePath = ConvertPathToUrl(SafeGetString(reader, "imagePath")),
                    Status = SafeGetBoolean(reader, "status", true),
                    AttributeValuesDisplay = "", // Would need additional query to get attribute values
                    FullDisplayName = $"{SafeGetString(reader, "parentProductName")} - {SafeGetString(reader, "name")}"
                };
            }
            catch
            {
                return null;
            }
        }

        private ItemDto? CreateVariationItemFromDataReader(SqliteDataReader reader)
        {
            try
            {
                var categoriesJson = SafeGetString(reader, "categories");
                var categories = new List<string>();
                
                if (!string.IsNullOrEmpty(categoriesJson))
                {
                    try
                    {
                        categories = JsonConvert.DeserializeObject<List<string>>(categoriesJson) ?? new List<string>();
                    }
                    catch
                    {
                        categories = new List<string>();
                    }
                }

                return new ItemDto
                {
                    Id = SafeGetString(reader, "Id"),
                    Name = SafeGetString(reader, "name"),
                    SKU = SafeGetString(reader, "sku"),
                    Type = "Variation",
                    Description = SafeGetString(reader, "description"),
                    Cost = SafeGetDecimal(reader, "cost"),
                    SalesPrice = SafeGetDecimal(reader, "salesPrice"),
                    ImagePath = ConvertPathToUrl(SafeGetString(reader, "imagePath")),
                    Status = SafeGetBoolean(reader, "status", true),
                    Taxable = SafeGetBoolean(reader, "taxable"),
                    TaxId = SafeGetString(reader, "taxId"),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    Categories = categories
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Modifier Operations

        public async Task<List<ModifierDto>> GetAllModifiersAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllModifiers, out List<ModifierDto>? cached) && cached != null)
                return cached;

            var modifiers = new List<ModifierDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT m.modifierId, m.modifierName, m.priceChange, m.sortOrder, m.status, 
                       m.createdDate, m.modifiedDate, m.recipeId, m.useRecipePrice,
                       b.name as recipeName, b.cost as recipeCost
                FROM Modifiers m
                LEFT JOIN BundleItems b ON m.recipeId = b.bundleId
                WHERE m.status = 1
                ORDER BY m.sortOrder, m.modifierName";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                modifiers.Add(new ModifierDto
                {
                    ModifierId = SafeGetString(reader, "modifierId"),
                    ModifierName = SafeGetString(reader, "modifierName"),
                    PriceChange = SafeGetDecimal(reader, "priceChange"),
                    SortOrder = SafeGetInt32(reader, "sortOrder"),
                    Status = SafeGetBoolean(reader, "status", true),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    RecipeId = SafeGetString(reader, "recipeId"),
                    UseRecipePrice = SafeGetBoolean(reader, "useRecipePrice"),
                    RecipeName = SafeGetString(reader, "recipeName"),
                    RecipeCost = SafeGetDecimal(reader, "recipeCost")
                });
            }

            _cache?.Set(CacheKeyAllModifiers, modifiers, ListCacheDuration);
            return modifiers;
        }

        public async Task<ModifierDto?> GetModifierByIdAsync(string modifierId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT m.modifierId, m.modifierName, m.priceChange, m.sortOrder, m.status, 
                       m.createdDate, m.modifiedDate, m.recipeId, m.useRecipePrice,
                       b.name as recipeName, b.cost as recipeCost
                FROM Modifiers m
                LEFT JOIN BundleItems b ON m.recipeId = b.bundleId
                WHERE m.modifierId = @modifierId AND m.status = 1";
            command.Parameters.AddWithValue("@modifierId", modifierId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ModifierDto
                {
                    ModifierId = SafeGetString(reader, "modifierId"),
                    ModifierName = SafeGetString(reader, "modifierName"),
                    PriceChange = SafeGetDecimal(reader, "priceChange"),
                    SortOrder = SafeGetInt32(reader, "sortOrder"),
                    Status = SafeGetBoolean(reader, "status", true),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    RecipeId = SafeGetString(reader, "recipeId"),
                    UseRecipePrice = SafeGetBoolean(reader, "useRecipePrice"),
                    RecipeName = SafeGetString(reader, "recipeName"),
                    RecipeCost = SafeGetDecimal(reader, "recipeCost")
                };
            }

            return null;
        }

        /// <summary>
        /// Get all modifiers associated with a specific product
        /// </summary>
        public async Task<List<ModifierDto>> GetProductModifiersAsync(string productId)
        {
            var modifiers = new List<ModifierDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // First, get the availableModifiers field from the product
            var productCommand = connection.CreateCommand();
            productCommand.CommandText = @"
                SELECT availableModifiers 
                FROM Products 
                WHERE productId = @productId AND status = 1";
            productCommand.Parameters.AddWithValue("@productId", productId);

            var availableModifiersStr = (await productCommand.ExecuteScalarAsync())?.ToString();
            
            if (string.IsNullOrEmpty(availableModifiersStr))
            {
                return modifiers; // No modifiers for this product
            }

            // Parse the comma-separated modifier IDs
            var modifierIds = availableModifiersStr.Split(',')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            if (modifierIds.Count == 0)
            {
                return modifiers;
            }

            // Build parameterized IN clause for modifier IDs
            var placeholders = string.Join(",", modifierIds.Select((_, i) => $"@modId{i}"));
            
            var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT m.modifierId, m.modifierName, m.priceChange, m.sortOrder, m.status, 
                       m.createdDate, m.modifiedDate, m.recipeId, m.useRecipePrice,
                       b.name as recipeName, b.cost as recipeCost
                FROM Modifiers m
                LEFT JOIN BundleItems b ON m.recipeId = b.bundleId
                WHERE m.modifierId IN ({placeholders}) AND m.status = 1
                ORDER BY m.sortOrder, m.modifierName";

            // Add parameters for each modifier ID
            for (int i = 0; i < modifierIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@modId{i}", modifierIds[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                modifiers.Add(new ModifierDto
                {
                    ModifierId = SafeGetString(reader, "modifierId"),
                    ModifierName = SafeGetString(reader, "modifierName"),
                    PriceChange = SafeGetDecimal(reader, "priceChange"),
                    SortOrder = SafeGetInt32(reader, "sortOrder"),
                    Status = SafeGetBoolean(reader, "status", true),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    RecipeId = SafeGetString(reader, "recipeId"),
                    UseRecipePrice = SafeGetBoolean(reader, "useRecipePrice"),
                    RecipeName = SafeGetString(reader, "recipeName"),
                    RecipeCost = SafeGetDecimal(reader, "recipeCost")
                });
            }

            return modifiers;
        }

        #endregion

        #region Multi-Unit Pricing Operations

        public async Task<List<ProductUnitPricingDto>> GetProductUnitPricingAsync(string productId)
        {
            var unitPricing = new List<ProductUnitPricingDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // First get the base product price
            var productCommand = connection.CreateCommand();
            productCommand.CommandText = "SELECT salesPrice FROM Products WHERE productId = @productId";
            productCommand.Parameters.AddWithValue("@productId", productId);
            var basePrice = Convert.ToDecimal(await productCommand.ExecuteScalarAsync() ?? 0);

            // Get unit pricing options
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT unitPricingId, productId, variationId, packageName, baseUnit, 
                       unitsPerPackage, packagePrice, sku, isActive, 
                       createdDate, modifiedDate
                FROM ProductUnitPricing
                WHERE productId = @productId AND isActive = 1
                ORDER BY unitsPerPackage ASC";
            command.Parameters.AddWithValue("@productId", productId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var pricing = new ProductUnitPricingDto
                {
                    UnitPricingId = SafeGetString(reader, "unitPricingId"),
                    ProductId = SafeGetString(reader, "productId"),
                    VariationId = SafeGetString(reader, "variationId"),
                    PackageName = SafeGetString(reader, "packageName"),
                    BaseUnit = SafeGetString(reader, "baseUnit"),
                    UnitsPerPackage = SafeGetDecimal(reader, "unitsPerPackage"),
                    PackagePrice = SafeGetDecimal(reader, "packagePrice"),
                    SKU = SafeGetString(reader, "sku"),
                    IsActive = SafeGetBoolean(reader, "isActive", true),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    BaseUnitPrice = basePrice
                };

                unitPricing.Add(pricing);
            }

            return unitPricing;
        }

        public async Task<List<ProductUnitPricingDto>> GetVariationUnitPricingAsync(string variationId)
        {
            var unitPricing = new List<ProductUnitPricingDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // First get the base variation price
            var variationCommand = connection.CreateCommand();
            variationCommand.CommandText = "SELECT salesPrice FROM ProductVariations WHERE variationId = @variationId";
            variationCommand.Parameters.AddWithValue("@variationId", variationId);
            var basePrice = Convert.ToDecimal(await variationCommand.ExecuteScalarAsync() ?? 0);

            // Get unit pricing options
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT unitPricingId, productId, variationId, packageName, baseUnit, 
                       unitsPerPackage, packagePrice, sku, isActive, 
                       createdDate, modifiedDate
                FROM ProductUnitPricing
                WHERE variationId = @variationId AND isActive = 1
                ORDER BY unitsPerPackage ASC";
            command.Parameters.AddWithValue("@variationId", variationId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var pricing = new ProductUnitPricingDto
                {
                    UnitPricingId = SafeGetString(reader, "unitPricingId"),
                    ProductId = SafeGetString(reader, "productId"),
                    VariationId = SafeGetString(reader, "variationId"),
                    PackageName = SafeGetString(reader, "packageName"),
                    BaseUnit = SafeGetString(reader, "baseUnit"),
                    UnitsPerPackage = SafeGetDecimal(reader, "unitsPerPackage"),
                    PackagePrice = SafeGetDecimal(reader, "packagePrice"),
                    SKU = SafeGetString(reader, "sku"),
                    IsActive = SafeGetBoolean(reader, "isActive", true),
                    CreatedDate = reader.GetDateTime("createdDate"),
                    ModifiedDate = reader.GetDateTime("modifiedDate"),
                    BaseUnitPrice = basePrice
                };

                unitPricing.Add(pricing);
            }

            return unitPricing;
        }

        #endregion
    }
}