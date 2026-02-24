using Microsoft.Data.Sqlite;
using PottaAPI.Models;

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
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT discountId, couponCode, discountName, discountType, percentage, flatRate, 
                           description, requiresApproval, isActive, validFrom, validUntil, usageLimit, 
                           usageCount, createdDate, modifiedDate
                    FROM Discounts 
                    WHERE isActive = 1
                    ORDER BY discountName";

                var discounts = new List<DiscountDTO>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    discounts.Add(MapDiscountFromReader(reader));
                }

                return discounts;
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
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT discountId, couponCode, discountName, discountType, percentage, flatRate, 
                           description, requiresApproval, isActive, validFrom, validUntil, usageLimit, 
                           usageCount, createdDate, modifiedDate
                    FROM Discounts 
                    WHERE couponCode = @couponCode AND isActive = 1";
                command.Parameters.AddWithValue("@couponCode", couponCode);

                using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    _logger.LogWarning("Discount not found for coupon code: {CouponCode}", couponCode);
                    return null;
                }

                return MapDiscountFromReader(reader);
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
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Discounts 
                    SET usageCount = usageCount + 1 
                    WHERE discountId = @discountId";
                command.Parameters.AddWithValue("@discountId", discountId);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing usage count: {DiscountId}", discountId);
                return false;
            }
        }

        private DiscountDTO MapDiscountFromReader(SqliteDataReader reader)
        {
            return new DiscountDTO
            {
                DiscountId = reader["discountId"]?.ToString() ?? string.Empty,
                CouponCode = reader["couponCode"]?.ToString() ?? string.Empty,
                DiscountName = reader["discountName"]?.ToString() ?? string.Empty,
                DiscountType = reader["discountType"]?.ToString() ?? string.Empty,
                Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDecimal(reader["percentage"]) : 0,
                FlatRate = reader["flatRate"] != DBNull.Value ? Convert.ToDecimal(reader["flatRate"]) : 0,
                Description = reader["description"]?.ToString() ?? string.Empty,
                RequiresApproval = reader["requiresApproval"] != DBNull.Value && Convert.ToBoolean(reader["requiresApproval"]),
                IsActive = reader["isActive"] != DBNull.Value && Convert.ToBoolean(reader["isActive"]),
                ValidFrom = reader["validFrom"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["validFrom"]) : null,
                ValidUntil = reader["validUntil"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["validUntil"]) : null,
                UsageLimit = reader["usageLimit"] != DBNull.Value ? Convert.ToInt32(reader["usageLimit"]) : 0,
                UsageCount = reader["usageCount"] != DBNull.Value ? Convert.ToInt32(reader["usageCount"]) : 0,
                CreatedDate = reader["createdDate"] != DBNull.Value ? Convert.ToDateTime(reader["createdDate"]) : DateTime.Now,
                ModifiedDate = reader["modifiedDate"] != DBNull.Value ? Convert.ToDateTime(reader["modifiedDate"]) : DateTime.Now
            };
        }
    }
}
