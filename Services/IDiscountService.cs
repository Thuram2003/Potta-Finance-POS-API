using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for discount operations on waiting transactions
    /// </summary>
    public interface IDiscountService
    {
        /// <summary>
        /// Get active discounts available for selection
        /// </summary>
        Task<List<DiscountDTO>> GetActiveDiscountsAsync();

        /// <summary>
        /// Validate and retrieve discount by coupon code
        /// </summary>
        Task<DiscountDTO?> GetDiscountByCouponCodeAsync(string couponCode);

        /// <summary>
        /// Increment usage count when discount is applied
        /// </summary>
        Task<bool> IncrementUsageCountAsync(string discountId);
    }
}
