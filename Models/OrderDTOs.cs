using System.ComponentModel.DataAnnotations;

namespace PottaAPI.Models
{
    /// <summary>
    /// Request model for creating a new waiting transaction (mobile order)
    /// Matches the WPF app's WaitingTransaction model
    /// </summary>
    /// <example>
    /// {
    ///   "staffId": 1,
    ///   "customerId": "CUST001",
    ///   "tableId": "TBL001",
    ///   "tableNumber": 5,
    ///   "tableName": "Table 5",
    ///   "items": [
    ///     {
    ///       "productId": "PROD001",
    ///       "name": "Burger",
    ///       "quantity": 2,
    ///       "price": 5000,
    ///       "discount": 0,
    ///       "taxId": "TAX001",
    ///       "taxable": true,
    ///       "unitType": "Base",
    ///       "unitsPerPackage": 1
    ///     }
    ///   ]
    /// }
    /// </example>
    public class CreateWaitingTransactionDto
    {
        /// <summary>
        /// Staff member ID creating the order (required)
        /// </summary>
        /// <example>1</example>
        [Required(ErrorMessage = "StaffId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "StaffId must be greater than 0")]
        public int StaffId { get; set; }

        /// <summary>
        /// Customer ID (optional)
        /// </summary>
        /// <example>CUST001</example>
        public string? CustomerId { get; set; }

        /// <summary>
        /// Table ID (optional)
        /// </summary>
        /// <example>TBL001</example>
        public string? TableId { get; set; }

        /// <summary>
        /// Table number (optional)
        /// </summary>
        /// <example>5</example>
        public int? TableNumber { get; set; }

        /// <summary>
        /// Table name (optional)
        /// </summary>
        /// <example>Table 5</example>
        public string? TableName { get; set; }

        /// <summary>
        /// List of order items (at least one required)
        /// </summary>
        [Required(ErrorMessage = "Items list is required")]
        [MinLength(1, ErrorMessage = "Order must contain at least one item")]
        public List<WaitingTransactionItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// Order item model for waiting transaction (cart items)
    /// Matches the WPF app's CartItem model EXACTLY
    /// </summary>
    /// <example>
    /// {
    ///   "productId": "PROD001",
    ///   "name": "Burger",
    ///   "quantity": 2,
    ///   "price": 5000,
    ///   "discount": 0,
    ///   "taxId": "TAX001",
    ///   "taxable": true,
    ///   "unitType": "Base",
    ///   "unitsPerPackage": 1,
    ///   "appliedModifiers": [
    ///     {
    ///       "modifierId": "MOD001",
    ///       "modifierName": "Extra Cheese",
    ///       "priceChange": 500
    ///     }
    ///   ]
    /// }
    /// </example>
    public class WaitingTransactionItemDto
    {
        /// <summary>
        /// Product ID (required)
        /// </summary>
        /// <example>PROD001</example>
        [Required(ErrorMessage = "ProductId is required")]
        public string ProductId { get; set; } = "";

        /// <summary>
        /// Product name (required)
        /// </summary>
        /// <example>Burger</example>
        [Required(ErrorMessage = "Product name is required")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Quantity ordered (minimum 1)
        /// </summary>
        /// <example>2</example>
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        /// <summary>
        /// Unit price
        /// </summary>
        /// <example>5000</example>
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }

        /// <summary>
        /// Discount amount
        /// </summary>
        /// <example>0</example>
        public decimal Discount { get; set; } = 0;

        /// <summary>
        /// Tax ID (optional)
        /// </summary>
        /// <example>TAX001</example>
        public string? TaxId { get; set; }

        /// <summary>
        /// Is item taxable
        /// </summary>
        /// <example>true</example>
        public bool Taxable { get; set; } = true;

        /// <summary>
        /// Staff ID (optional)
        /// </summary>
        /// <example>1</example>
        public int? StaffId { get; set; }

        /// <summary>
        /// Is item completed
        /// </summary>
        /// <example>false</example>
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// Creation date
        /// </summary>
        /// <example>2024-01-15T10:30:00</example>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Applied modifiers (optional)
        /// </summary>
        public List<AppliedModifierDto>? AppliedModifiers { get; set; }

        /// <summary>
        /// Modifier selection ID (optional)
        /// </summary>
        /// <example>MODSEL001</example>
        public string? ModifierSelectionId { get; set; }
        
        /// <summary>
        /// Unit type for multi-unit pricing
        /// </summary>
        /// <example>Base</example>
        public string UnitType { get; set; } = "Base";

        /// <summary>
        /// Units per package for inventory tracking
        /// </summary>
        /// <example>1</example>
        public decimal UnitsPerPackage { get; set; } = 1;
        
        /// <summary>
        /// Is this a bundle item
        /// </summary>
        /// <example>false</example>
        public bool IsBundle { get; set; } = false;

        /// <summary>
        /// Is this a recipe item
        /// </summary>
        /// <example>false</example>
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
    /// Applied modifier model (matches WPF app's AppliedModifier model)
    /// </summary>
    /// <example>
    /// {
    ///   "modifierId": "MOD001",
    ///   "modifierName": "Extra Cheese",
    ///   "priceChange": 500,
    ///   "recipeId": null
    /// }
    /// </example>
    public class AppliedModifierDto
    {
        /// <summary>
        /// Modifier ID
        /// </summary>
        /// <example>MOD001</example>
        public string ModifierId { get; set; } = "";

        /// <summary>
        /// Modifier name
        /// </summary>
        /// <example>Extra Cheese</example>
        public string ModifierName { get; set; } = "";

        /// <summary>
        /// Price change (can be positive or negative)
        /// </summary>
        /// <example>500</example>
        public decimal PriceChange { get; set; } = 0;

        /// <summary>
        /// Recipe ID (optional)
        /// </summary>
        /// <example>null</example>
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
