using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Data;
using Newtonsoft.Json;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for item-related database operations (products, bundles, recipes, variations)
    /// </summary>
    public class ItemService : IItemService
    {
        private readonly string _connectionString;

        public ItemService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region General Item Operations

        public async Task<List<ItemDto>> GetAllItemsAsync()
        {
            var items = new List<ItemDto>();

            // Get products
            var products = await GetAllProductsAsync();
            items.AddRange(products.Cast<ItemDto>());

            // Get bundles
            var bundles = await GetAllBundlesAsync();
            items.AddRange(bundles.Cast<ItemDto>());

            return items.OrderBy(i => i.Name).ToList();
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
            var totalCount = 0;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = new List<SqliteParameter>();

            // Build WHERE clause - MATCHES DESKTOP IMPLEMENTATION
            // Always: status = 1 (active items only)
            // Products: isIngredient = 0 (exclude ingredients)
            // Search: name, sku, description, categories
            
            string productWhereClause;
            string bundleWhereClause;

            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                // Search with term - matches Desktop SearchProducts and SearchBundleItems
                productWhereClause = @"WHERE status = 1 AND isIngredient = 0 AND (
                    name LIKE @searchTerm OR 
                    sku LIKE @searchTerm OR 
                    description LIKE @searchTerm OR
                    categories LIKE @searchTerm
                )";

                bundleWhereClause = @"WHERE status = 1 AND (
                    name LIKE @searchTerm OR 
                    sku LIKE @searchTerm OR 
                    description LIKE @searchTerm
                )";

                parameters.Add(new SqliteParameter("@searchTerm", $"%{searchRequest.SearchTerm}%"));
            }
            else
            {
                // No search term - return all active items (matches Desktop GetAllProducts and GetAllBundleItems)
                productWhereClause = "WHERE status = 1 AND isIngredient = 0";
                bundleWhereClause = "WHERE status = 1";
            }

            // Search in products (parent products only, not variations)
            // MATCHES: ProductDatabaseService.SearchProducts()
            var productQuery = $@"
                SELECT productId as Id, name, sku, 'Product' as type, description, cost, salesPrice, 
                       imagePath, status, taxable, taxId, createdDate, modifiedDate,
                       inventoryOnHand, reorderPoint, unitOfMeasure, categories,
                       hasVariations, variationCount, isIngredient, costPerUnit, 
                       purchaseUnit, recipeUnit, conversionFactor, purchaseMode
                FROM Products 
                {productWhereClause}
                ORDER BY name";

            await ExecuteSearchQuery(connection, productQuery, parameters, items, "Product");

            // Search in bundles (including recipes)
            // MATCHES: BundleItemDatabaseService.SearchBundleItems()
            var bundleQuery = $@"
                SELECT bundleId as Id, name, sku, 
                       CASE WHEN isRecipe = 1 THEN 'Recipe' ELSE 'Bundle' END as type,
                       description, cost, salesPrice, imagePath, status, taxable, taxId, 
                       createdDate, modifiedDate, structure, inventoryOnHand, reorderPoint,
                       isRecipe, servingSize, preparationTime, cookingInstructions
                FROM BundleItems 
                {bundleWhereClause}
                ORDER BY name";

            await ExecuteSearchQuery(connection, bundleQuery, parameters, items, "Bundle");

            totalCount = items.Count;

            // Apply pagination
            var offset = (searchRequest.Page - 1) * searchRequest.PageSize;
            items = items.Skip(offset).Take(searchRequest.PageSize).ToList();

            return new ItemSearchResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = searchRequest.Page,
                PageSize = searchRequest.PageSize,
                ItemType = "", // Not used in simplified search
                Category = "" // Not used in simplified search
            };
        }

        private async Task ExecuteSearchQuery(SqliteConnection connection, string query, List<SqliteParameter> parameters, List<ItemDto> items, string itemType)
        {
            var command = connection.CreateCommand();
            command.CommandText = query;
            
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
                WHERE status = 1
                ORDER BY name";

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
                if (product != null && product.HasVariations)
                {
                    product.Variations = await GetProductVariationsAsync(productId);
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
            var result = new ProductVariationsWithAttributesDto
            {
                ProductId = productId
            };

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get product name
            var productCommand = connection.CreateCommand();
            productCommand.CommandText = "SELECT name FROM Products WHERE productId = @productId";
            productCommand.Parameters.AddWithValue("@productId", productId);
            result.ProductName = (await productCommand.ExecuteScalarAsync())?.ToString() ?? "";

            // Get all attributes for this product
            var attributesCommand = connection.CreateCommand();
            attributesCommand.CommandText = @"
                SELECT DISTINCT pa.attributeId, pa.attributeName
                FROM ProductAttributes pa
                WHERE pa.productId = @productId
                ORDER BY pa.attributeName";
            attributesCommand.Parameters.AddWithValue("@productId", productId);

            using (var attributesReader = await attributesCommand.ExecuteReaderAsync())
            {
                while (await attributesReader.ReadAsync())
                {
                    var attribute = new ProductAttributeDto
                    {
                        AttributeId = attributesReader["attributeId"]?.ToString() ?? "",
                        AttributeName = attributesReader["attributeName"]?.ToString() ?? ""
                    };

                    // Get all values for this attribute
                    var valuesCommand = connection.CreateCommand();
                    valuesCommand.CommandText = @"
                        SELECT DISTINCT pav.valueId, pav.valueName
                        FROM ProductAttributeValues pav
                        WHERE pav.attributeId = @attributeId
                        ORDER BY pav.valueName";
                    valuesCommand.Parameters.AddWithValue("@attributeId", attribute.AttributeId);

                    using (var valuesReader = await valuesCommand.ExecuteReaderAsync())
                    {
                        while (await valuesReader.ReadAsync())
                        {
                            attribute.Values.Add(new ProductAttributeValueDto
                            {
                                ValueId = valuesReader["valueId"]?.ToString() ?? "",
                                ValueName = valuesReader["valueName"]?.ToString() ?? ""
                            });
                        }
                    }

                    result.Attributes.Add(attribute);
                }
            }

            // Get all variations
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
            var bundles = new List<BundleDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT bundleId, name, sku, structure, description, cost, salesPrice, imagePath,
                       inventoryOnHand, reorderPoint, status, taxable, taxId, createdDate, modifiedDate,
                       isRecipe, servingSize, preparationTime, cookingInstructions
                FROM BundleItems 
                WHERE status = 1
                ORDER BY name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bundle = CreateBundleFromDataReader(reader);
                if (bundle != null)
                {
                    bundles.Add(bundle);
                }
            }

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
                if (recipe != null)
                {
                    recipes.Add(recipe);
                }
            }

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
                    ImagePath = reader["imagePath"]?.ToString() ?? "",
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
                    ImagePath = SafeGetString(reader, "imagePath"),
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
                    ImagePath = SafeGetString(reader, "imagePath"),
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
                    ImagePath = SafeGetString(reader, "imagePath"),
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
    }
}