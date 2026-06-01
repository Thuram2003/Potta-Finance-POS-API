using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for bundle and recipe operations using Dapper
    /// </summary>
    public class BundleService : IBundleService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache? _cache;

        private const string CacheKeyAllBundles = "bundles:all";
        private const string CacheKeyAllRecipes = "bundles:recipes";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public BundleService(
            IConnectionStringProvider connectionStringProvider,
            IMemoryCache? cache = null)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _cache = cache;
        }

        public async Task<List<BundleDto>> GetAllBundlesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllBundles, out List<BundleDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT b.bundleId, b.name, b.sku, b.structure, b.description, b.cost, b.salesPrice, b.imagePath,
                       b.inventoryOnHand, b.reorderPoint, b.status, b.taxable, b.taxId, b.createdDate, b.modifiedDate,
                       b.isRecipe, b.servingSize, b.preparationTime, b.cookingInstructions,
                       t.taxName, t.taxType, t.percentage, t.flatRate
                FROM BundleItems b
                LEFT JOIN Taxes t ON b.taxId = t.taxId AND t.isActive = 1
                WHERE b.status = 1 AND b.isRecipe = 0
                ORDER BY b.name";

            var bundles = (await connection.QueryAsync<BundleDto>(sql)).ToList();

            foreach (var bundle in bundles)
            {
                bundle.Type = "Bundle";
                bundle.ImagePath = ConvertPathToUrl(bundle.ImagePath);
                bundle.Categories = new List<string>();
                bundle.HasVariations = false;
                bundle.VariationCount = 0;
            }

            _cache?.Set(CacheKeyAllBundles, bundles, CacheDuration);
            return bundles;
        }

        public async Task<BundleDto?> GetBundleByIdAsync(string bundleId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT b.bundleId, b.name, b.sku, b.structure, b.description, b.cost, b.salesPrice, b.imagePath,
                       b.inventoryOnHand, b.reorderPoint, b.status, b.taxable, b.taxId, b.createdDate, b.modifiedDate,
                       b.isRecipe, b.servingSize, b.preparationTime, b.cookingInstructions,
                       t.taxId as tax_taxId, t.taxName, t.taxType, t.description as tax_description,
                       t.percentage, t.flatRate, t.percentageCap, t.isActive as tax_isActive,
                       t.createdDate as tax_createdDate, t.modifiedDate as tax_modifiedDate
                FROM BundleItems b
                LEFT JOIN Taxes t ON b.taxId = t.taxId AND t.isActive = 1
                WHERE b.bundleId = @bundleId AND b.status = 1";

            var bundle = await connection.QueryFirstOrDefaultAsync<BundleDto>(sql, new { bundleId });
            if (bundle == null) return null;

            bundle.Type = bundle.IsRecipe ? "Recipe" : "Bundle";
            bundle.ImagePath = ConvertPathToUrl(bundle.ImagePath);
            bundle.Categories = new List<string>();
            bundle.HasVariations = false;
            bundle.VariationCount = 0;

            // Load components
            bundle.Components = await GetBundleComponentsAsync(bundleId);

            // Load modifiers
            bundle.Modifiers = await GetBundleModifiersAsync(bundleId);

            // Populate full Tax object if tax exists
            var taxIdValue = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT taxId FROM Taxes WHERE taxId = @taxId AND isActive = 1",
                new { taxId = bundle.TaxId });

            if (!string.IsNullOrEmpty(taxIdValue))
            {
                var tax = await connection.QueryFirstOrDefaultAsync<TaxDTO>(
                    @"SELECT taxId, taxName, taxType, description, percentage, flatRate, 
                             percentageCap, isActive, createdDate, modifiedDate
                      FROM Taxes WHERE taxId = @taxId AND isActive = 1",
                    new { taxId = taxIdValue });

                if (tax != null)
                {
                    bundle.Tax = tax;
                }
            }

            return bundle;
        }

        public async Task<List<BundleDto>> GetAllRecipesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllRecipes, out List<BundleDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT b.bundleId, b.name, b.sku, b.structure, b.description, b.cost, b.salesPrice, b.imagePath,
                       b.inventoryOnHand, b.reorderPoint, b.status, b.taxable, b.taxId, b.createdDate, b.modifiedDate,
                       b.isRecipe, b.servingSize, b.preparationTime, b.cookingInstructions,
                       t.taxName, t.taxType, t.percentage, t.flatRate
                FROM BundleItems b
                LEFT JOIN Taxes t ON b.taxId = t.taxId AND t.isActive = 1
                WHERE b.status = 1 AND b.isRecipe = 1
                ORDER BY b.name";

            var recipes = (await connection.QueryAsync<BundleDto>(sql)).ToList();

            foreach (var recipe in recipes)
            {
                recipe.Type = "Recipe";
                recipe.ImagePath = ConvertPathToUrl(recipe.ImagePath);
                recipe.Categories = new List<string>();
                recipe.HasVariations = false;
                recipe.VariationCount = 0;
            }

            _cache?.Set(CacheKeyAllRecipes, recipes, CacheDuration);
            return recipes;
        }

        public async Task<List<BundleComponentDto>> GetBundleComponentsAsync(string bundleId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT bc.productId, bc.quantity, bc.recipeUnit, p.name as productName, p.salesPrice
                FROM BundleComponents bc
                INNER JOIN Products p ON bc.productId = p.productId
                WHERE bc.bundleId = @bundleId
                ORDER BY p.name";

            var components = (await connection.QueryAsync<BundleComponentDto>(sql, new { bundleId })).ToList();

            foreach (var component in components)
            {
                component.ProductPrice = component.ProductPrice;
            }

            return components;
        }

        public async Task<List<ModifierDto>> GetBundleModifiersAsync(string bundleId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get the availableModifiers field from the bundle
            var availableModifiersStr = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT availableModifiers FROM BundleItems WHERE bundleId = @bundleId AND status = 1",
                new { bundleId });

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
