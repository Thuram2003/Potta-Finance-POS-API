using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Discount endpoints for applying discounts to waiting transactions
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class DiscountsController : ControllerBase
    {
        private readonly IDiscountService _discountService;
        private readonly ILogger<DiscountsController> _logger;

        public DiscountsController(IDiscountService discountService, ILogger<DiscountsController> logger)
        {
            _discountService = discountService;
            _logger = logger;
        }

        /// <summary>
        /// Get active discounts available for selection
        /// </summary>
        [HttpGet("active")]
        [ProducesResponseType(typeof(List<DiscountDTO>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DiscountDTO>>> GetActiveDiscounts()
        {
            try
            {
                var discounts = await _discountService.GetActiveDiscountsAsync();
                _logger.LogInformation("Retrieved {Count} active discounts", discounts.Count);
                return Ok(discounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active discounts");
                return StatusCode(500, new { error = "Failed to retrieve active discounts", details = ex.Message });
            }
        }

        /// <summary>
        /// Validate and retrieve discount by coupon code
        /// </summary>
        [HttpGet("coupon/{couponCode}")]
        [ProducesResponseType(typeof(DiscountDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DiscountDTO>> GetDiscountByCouponCode(string couponCode)
        {
            try
            {
                var discount = await _discountService.GetDiscountByCouponCodeAsync(couponCode);

                if (discount == null)
                {
                    return NotFound(new { error = "Discount not found", couponCode });
                }

                if (!discount.IsCurrentlyValid)
                {
                    return BadRequest(new { error = "Discount is not currently valid", couponCode });
                }

                return Ok(discount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discount by coupon code: {CouponCode}", couponCode);
                return StatusCode(500, new { error = "Failed to retrieve discount", details = ex.Message });
            }
        }

        /// <summary>
        /// Increment usage count when discount is applied to a transaction
        /// </summary>
        [HttpPost("{discountId}/increment-usage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> IncrementUsageCount(string discountId)
        {
            try
            {
                var result = await _discountService.IncrementUsageCountAsync(discountId);

                if (!result)
                {
                    return NotFound(new { error = "Discount not found", discountId });
                }

                _logger.LogInformation("Incremented usage count for discount: {DiscountId}", discountId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing usage count for discount: {DiscountId}", discountId);
                return StatusCode(500, new { error = "Failed to increment usage count", details = ex.Message });
            }
        }
    }
}
