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
}