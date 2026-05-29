using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using Dapper;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for discount operations on waiting transactions
    /// </summary>
    public class DiscountService : IDiscountService
    {
        private readonly string _connectionString;
        private readonly ILogger<DiscountService> _logger;

        public DiscountService(IConnectionStringProvider connectionStringProvider, ILogger<DiscountService> logger)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _logger = logger;
        }

        public async Task<List<DiscountDTO>> GetActiveDiscountsAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT discountId, couponCode, discountName, discountType, percentage, flatRate, 
                           description, requiresApproval, isActive, validFrom, validUntil, usageLimit, 
                           usageCount, createdDate, modifiedDate
                    FROM Discounts 
                    WHERE isActive = 1
                    ORDER BY discountName";

                var discounts = await connection.QueryAsync<DiscountDTO>(sql);
                return discounts.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active discounts");
                return new List<DiscountDTO>();
            }
        }

        public async Task<DiscountDTO?> GetDiscountByCouponCodeAsync(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
                return null;

            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT discountId, couponCode, discountName, discountType, percentage, flatRate, 
                           description, requiresApproval, isActive, validFrom, validUntil, usageLimit, 
                           usageCount, createdDate, modifiedDate
                    FROM Discounts 
                    WHERE couponCode = @couponCode AND isActive = 1";

                var discount = await connection.QueryFirstOrDefaultAsync<DiscountDTO>(sql, new { couponCode });

                if (discount == null)
                {
                    _logger.LogWarning("Discount not found for coupon code: {CouponCode}", couponCode);
                }

                return discount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discount by coupon code: {CouponCode}", couponCode);
                return null;
            }
        }

        public async Task<bool> IncrementUsageCountAsync(string discountId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    UPDATE Discounts 
                    SET usageCount = usageCount + 1 
                    WHERE discountId = @discountId";

                var result = await connection.ExecuteAsync(sql, new { discountId });
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing usage count: {DiscountId}", discountId);
                return false;
            }
        }
    }
}
