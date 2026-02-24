using System;

namespace PottaAPI.Models
{
    /// <summary>
    /// Discount configuration DTO for applying discounts to waiting transactions
    /// </summary>
    public class DiscountDTO
    {
        public string DiscountId { get; set; } = string.Empty;
        public string CouponCode { get; set; } = string.Empty;
        public string DiscountName { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal FlatRate { get; set; }
        public string? Description { get; set; }
        public bool RequiresApproval { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public int UsageLimit { get; set; }
        public int UsageCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        /// <summary>
        /// Display text for UI (e.g., "10%" or "XAF 500")
        /// </summary>
        public string DisplayText => DiscountType == "Percentage" 
            ? $"{Percentage}%" 
            : $"XAF {FlatRate:N0}";

        /// <summary>
        /// Check if discount is currently valid
        /// </summary>
        public bool IsCurrentlyValid
        {
            get
            {
                if (!IsActive) return false;
                
                var now = DateTime.Now;
                if (ValidFrom.HasValue && now < ValidFrom.Value) return false;
                if (ValidUntil.HasValue && now > ValidUntil.Value) return false;
                if (UsageLimit > 0 && UsageCount >= UsageLimit) return false;
                
                return true;
            }
        }
    }
}
