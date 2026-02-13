using System.ComponentModel.DataAnnotations;

namespace PottaAPI.Models
{
    /// <summary>
    /// Base item data transfer object for API responses
    /// </summary>
    public class ItemDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string SKU { get; set; } = "";
        public string Type { get; set; } = ""; // "Product", "Bundle", "Recipe"
        public string Description { get; set; } = "";
        public decimal Cost { get; set; }
        public decimal SalesPrice { get; set; }
        public string FormattedPrice => $"XAF {SalesPrice:N0}";
        public string ImagePath { get; set; } = "";
        public bool Status { get; set; }
        public bool Taxable { get; set; }
        public string TaxId { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string StatusDisplay => Status ? "Active" : "Inactive";
        public List<string> Categories { get; set; } = new();
        public string CategoriesDisplay => Categories.Any() ? string.Join(", ", Categories) : "Uncategorized";
    }

    /// <summary>
    /// Product-specific data transfer object
    /// </summary>
    public class ProductDto : ItemDto
    {
        public decimal InventoryOnHand { get; set; }
        public decimal ReorderPoint { get; set; }
        public string UnitOfMeasure { get; set; } = "";
        public bool HasVariations { get; set; }
        public int VariationCount { get; set; }
        public bool IsIngredient { get; set; }
        public decimal CostPerUnit { get; set; }
        public string PurchaseUnit { get; set; } = "";
        public string RecipeUnit { get; set; } = "";
        public decimal ConversionFactor { get; set; } = 1;
        public string PurchaseMode { get; set; } = "Standard";
        public bool IsLowStock => InventoryOnHand < ReorderPoint && ReorderPoint > 0;
        public string StockInfo => IsLowStock ? $"Low Stock ({InventoryOnHand:F2})" : $"{InventoryOnHand:F2} in stock";
        public string VariationDisplay => HasVariations ? $"{VariationCount} variations" : "";
        public List<ProductVariationDto> Variations { get; set; } = new();
    }

    /// <summary>
    /// Product variation data transfer object
    /// </summary>
    public class ProductVariationDto
    {
        public string VariationId { get; set; } = "";
        public string ParentProductId { get; set; } = "";
        public string SKU { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Cost { get; set; }
        public decimal SalesPrice { get; set; }
        public string FormattedPrice => $"XAF {SalesPrice:N0}";
        public decimal InventoryOnHand { get; set; }
        public decimal ReorderPoint { get; set; }
        public string ImagePath { get; set; } = "";
        public bool Status { get; set; }
        public string AttributeValuesDisplay { get; set; } = "";
        public string FullDisplayName { get; set; } = "";
        public bool IsLowStock => InventoryOnHand < ReorderPoint && ReorderPoint > 0;
        public string StockInfo => IsLowStock ? $"Low Stock ({InventoryOnHand:F2})" : $"{InventoryOnHand:F2} in stock";
        
        // Attribute-value mappings for this variation (attributeId -> valueId)
        public Dictionary<string, string> AttributeValues { get; set; } = new();
    }

    /// <summary>
    /// Product attribute data transfer object (for variation attributes)
    /// </summary>
    public class ProductAttributeDto
    {
        public string AttributeId { get; set; } = "";
        public string AttributeName { get; set; } = "";
        public List<ProductAttributeValueDto> Values { get; set; } = new();
    }

    /// <summary>
    /// Product attribute value data transfer object
    /// </summary>
    public class ProductAttributeValueDto
    {
        public string ValueId { get; set; } = "";
        public string ValueName { get; set; } = "";
    }

    /// <summary>
    /// Product variations with attributes response (for ComboBox UI)
    /// </summary>
    public class ProductVariationsWithAttributesDto
    {
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public List<ProductAttributeDto> Attributes { get; set; } = new();
        public List<ProductVariationDto> Variations { get; set; } = new();
    }

    /// <summary>
    /// Bundle-specific data transfer object
    /// </summary>
    public class BundleDto : ItemDto
    {
        public string Structure { get; set; } = "Assembly"; // "Assembly" or "Bundle"
        public decimal InventoryOnHand { get; set; }
        public decimal ReorderPoint { get; set; }
        public bool IsRecipe { get; set; }
        public int ServingSize { get; set; } = 1;
        public int PreparationTime { get; set; }
        public string CookingInstructions { get; set; } = "";
        public bool IsLowStock => InventoryOnHand < ReorderPoint && ReorderPoint > 0;
        public string BundleInfo => IsRecipe ? $"Recipe ({ServingSize} servings)" : $"Bundle ({Components.Count} items)";
        public string RecipeTypeDisplay => IsRecipe ? "Recipe" : "Bundle";
        public List<BundleComponentDto> Components { get; set; } = new();
    }

    /// <summary>
    /// Bundle component data transfer object
    /// </summary>
    public class BundleComponentDto
    {
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string RecipeUnit { get; set; } = "";
        public decimal ProductPrice { get; set; }
        public decimal TotalPrice => ProductPrice * Quantity;
        public string FormattedTotalPrice => $"XAF {TotalPrice:N0}";
    }

    /// <summary>
    /// Item search request DTO
    /// </summary>
    public class ItemSearchDto
    {
        public string SearchTerm { get; set; } = "";
        public string Type { get; set; } = ""; // "Product", "Bundle", "Recipe", or empty for all
        public string Category { get; set; } = "";
        public bool IncludeInactive { get; set; } = false;
        public bool IncludeVariations { get; set; } = false;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Item search response DTO
    /// </summary>
    public class ItemSearchResponseDto
    {
        public List<ItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
        public string ItemType { get; set; } = "";
        public string Category { get; set; } = "";
    }

    /// <summary>
    /// Item statistics DTO
    /// </summary>
    public class ItemStatisticsDto
    {
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }
        public int InactiveItems { get; set; }
        public int TotalProducts { get; set; }
        public int TotalBundles { get; set; }
        public int TotalRecipes { get; set; }
        public int ProductsWithVariations { get; set; }
        public int TotalVariations { get; set; }
        public int LowStockItems { get; set; }
        public int IngredientsCount { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public string FormattedInventoryValue => $"XAF {TotalInventoryValue:N0}";
        public DateTime? LastItemCreated { get; set; }
        public string MostPopularCategory { get; set; } = "";
    }

    /// <summary>
    /// Category DTO for items
    /// </summary>
    public class CategoryDto
    {
        public string CategoryId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int ItemCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// Modifier DTO for product customizations
    /// </summary>
    public class ModifierDto
    {
        public string ModifierId { get; set; } = "";
        public string ModifierName { get; set; } = "";
        public decimal PriceChange { get; set; }
        public int SortOrder { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        
        // Recipe linking
        public string? RecipeId { get; set; }
        public bool UseRecipePrice { get; set; }
        public string? RecipeName { get; set; }
        public decimal RecipeCost { get; set; }
        
        // Computed properties
        public bool HasRecipe => !string.IsNullOrEmpty(RecipeId);
        public string ModifierTypeDisplay => HasRecipe ? "Recipe-based" : "Price Only";
        public decimal EffectivePrice => (HasRecipe && UseRecipePrice) ? RecipeCost : PriceChange;
        public string PriceChangeDisplay => 
            EffectivePrice >= 0 ? $"+XAF {EffectivePrice:N0}" : $"-XAF {Math.Abs(EffectivePrice):N0}";
        public string StatusDisplay => Status ? "Active" : "Inactive";
    }

    /// <summary>
    /// Multi-unit pricing DTO for package options
    /// </summary>
    public class ProductUnitPricingDto
    {
        public string UnitPricingId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string? VariationId { get; set; }
        public string PackageName { get; set; } = "";
        public string BaseUnit { get; set; } = "";
        public decimal UnitsPerPackage { get; set; }
        public decimal PackagePrice { get; set; }
        public string FormattedPackagePrice => $"XAF {PackagePrice:N0}";
        public string PackageImagePath { get; set; } = "";
        public bool IsActive { get; set; }
        public string SKU { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        
        // Computed properties
        public decimal PricePerUnitInPackage => UnitsPerPackage > 0 ? PackagePrice / UnitsPerPackage : 0;
        public string FormattedPricePerUnit => $"XAF {PricePerUnitInPackage:N0}";
        public decimal BaseUnitPrice { get; set; } // Set externally for comparison
        public decimal DiscountPercentage => 
            BaseUnitPrice > 0 && UnitsPerPackage > 0 
                ? ((BaseUnitPrice - PricePerUnitInPackage) / BaseUnitPrice) * 100 
                : 0;
        public bool IsDiscounted => DiscountPercentage > 0;
        public bool IsPremium => DiscountPercentage < 0;
        public string DiscountDisplay => 
            IsDiscounted ? $"Save {DiscountPercentage:F1}%" : 
            IsPremium ? $"+{Math.Abs(DiscountPercentage):F1}%" : "";
        public string PackageInfo => $"{UnitsPerPackage} {BaseUnit}s per {PackageName}";
    }
}
