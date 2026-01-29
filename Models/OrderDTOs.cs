using System.ComponentModel.DataAnnotations;

namespace PottaAPI.Models
{
    /// <summary>
    /// DTO for creating a new waiting transaction (mobile order)
    /// Matches the WPF app's WaitingTransaction model
    /// </summary>
    public class CreateWaitingTransactionDto
    {
        [Required(ErrorMessage = "StaffId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "StaffId must be greater than 0")]
        public int StaffId { get; set; }

        public string? CustomerId { get; set; }
        public string? TableId { get; set; }
        public int? TableNumber { get; set; }
        public string? TableName { get; set; }

        [Required(ErrorMessage = "Items list is required")]
        [MinLength(1, ErrorMessage = "Order must contain at least one item")]
        public List<WaitingTransactionItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO for waiting transaction items (cart items)
    /// Matches the WPF app's CartItem model EXACTLY
    /// </summary>
    public class WaitingTransactionItemDto
    {
        [Required(ErrorMessage = "ProductId is required")]
        public string ProductId { get; set; } = "";

        [Required(ErrorMessage = "Product name is required")]
        public string Name { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }

        public decimal Discount { get; set; } = 0;
        public string? TaxId { get; set; }
        public bool Taxable { get; set; } = true;
        public int? StaffId { get; set; }
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Modifier support (matches WPF app exactly)
        public List<AppliedModifierDto>? AppliedModifiers { get; set; }
        public string? ModifierSelectionId { get; set; }
        
        // Multi-unit pricing support
        public string UnitType { get; set; } = "Base";
        public decimal UnitsPerPackage { get; set; } = 1;
        
        // Bundle/Recipe flags
        public bool IsBundle { get; set; } = false;
        public bool IsRecipe { get; set; } = false;

        // Calculated properties (matches WPF app)
        public decimal SubTotal => (Price * Quantity) - Discount;
        public decimal Total { get; set; } // Includes modifiers and tax
        public decimal TaxAmount { get; set; } = 0;
        
        // Base unit quantity for inventory tracking
        public decimal BaseUnitQuantity => Quantity * UnitsPerPackage;
        
        // Helper property for modifiers JSON (for database storage)
        public string? ModifiersJson
        {
            get
            {
                if (AppliedModifiers == null || AppliedModifiers.Count == 0)
                    return null;
                return System.Text.Json.JsonSerializer.Serialize(AppliedModifiers);
            }
        }
        
        // Helper property to check if item has modifiers
        public bool HasModifiers => AppliedModifiers != null && AppliedModifiers.Count > 0;
    }

    /// <summary>
    /// DTO for applied modifiers (matches WPF app's AppliedModifier model)
    /// </summary>
    public class AppliedModifierDto
    {
        public string ModifierId { get; set; } = "";
        public string ModifierName { get; set; } = "";
        public decimal PriceChange { get; set; } = 0;
        public string? RecipeId { get; set; }
    }

    /// <summary>
    /// DTO for retrieving waiting transactions
    /// Matches the WPF app's WaitingTransaction display model
    /// </summary>
    public class WaitingTransactionDto
    {
        public string TransactionId { get; set; } = "";
        public string? CustomerId { get; set; }
        public string? TableId { get; set; }
        public int? TableNumber { get; set; }
        public string? TableName { get; set; }
        public int? StaffId { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Display properties
        public string TableDisplay => !string.IsNullOrEmpty(TableName) 
            ? TableName 
            : TableNumber.HasValue 
                ? $"Table {TableNumber}" 
                : "No Table";

        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - CreatedDate;
                if (timeSpan.TotalMinutes < 1) return "Just now";
                if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
                return $"{(int)timeSpan.TotalDays}d ago";
            }
        }

        // Items are loaded separately to avoid large payloads
        public List<WaitingTransactionItemDto> Items { get; set; } = new();
        
        // Summary properties - calculated from actual item data
        public int TotalItems => Items.Sum(i => i.Quantity);
        
        // Calculate total from item prices + modifiers (matches WPF app logic)
        public decimal TotalAmount
        {
            get
            {
                decimal total = 0;
                foreach (var item in Items)
                {
                    // Base subtotal: (Price × Quantity) - Discount
                    decimal itemSubTotal = (item.Price * item.Quantity) - item.Discount;
                    
                    // Add modifier costs
                    decimal modifierTotal = item.AppliedModifiers?.Sum(m => m.PriceChange) ?? 0;
                    
                    total += itemSubTotal + modifierTotal;
                }
                return total;
            }
        }
        
        public string FormattedTotal => $"XAF {TotalAmount:N0}";
    }

    /// <summary>
    /// DTO for updating transaction status
    /// </summary>
    public class UpdateTransactionStatusDto
    {
        [Required(ErrorMessage = "Status is required")]
        public string Status { get; set; } = "";
    }

    /// <summary>
    /// DTO for order statistics
    /// </summary>
    public class OrderStatisticsDto
    {
        public int PendingOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int TotalOrders { get; set; }
        public DateTime? OldestPendingOrder { get; set; }
        public DateTime? NewestOrder { get; set; }
        public int OrdersToday { get; set; }
        
        // Note: TotalOrderValue should be calculated from actual cart items, not stored
        // This is set by the service layer after deserializing cart items
        public decimal TotalOrderValue { get; set; }
        public string FormattedTotalValue => $"XAF {TotalOrderValue:N0}";
        
        public decimal OrderValueToday { get; set; }
        public string FormattedValueToday => $"XAF {OrderValueToday:N0}";
    }

    /// <summary>
    /// DTO for order summary by staff
    /// </summary>
    public class StaffOrderSummaryDto
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; } = "";
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal TotalOrderValue { get; set; }
        public string FormattedTotalValue => $"XAF {TotalOrderValue:N0}";
        public DateTime? LastOrderDate { get; set; }
    }

    /// <summary>
    /// DTO for order summary by table
    /// </summary>
    public class TableOrderSummaryDto
    {
        public string TableId { get; set; } = "";
        public string TableName { get; set; } = "";
        public int TableNumber { get; set; }
        public int ActiveOrders { get; set; }
        public decimal TotalOrderValue { get; set; }
        public string FormattedTotalValue => $"XAF {TotalOrderValue:N0}";
        public DateTime? FirstOrderTime { get; set; }
        public DateTime? LastOrderTime { get; set; }
        public string Status { get; set; } = "Available";
    }
}
