using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Discount lookup and usage tracking. Discounts are created on the desktop app.
    /// Mobile uses these endpoints to list available discounts, validate coupon codes,
    /// and record when a discount is applied to a transaction.
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

        /// <summary>Get all discounts that are currently active and within their validity period.</summary>
        /// <remarks>Use this to populate a discount picker on the checkout screen.</remarks>
        /// <response code="200">List of active discounts</response>
        /// <response code="500">Database error</response>
        [HttpGet("active")]
        [ProducesResponseType(typeof(List<DiscountDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>Look up a discount by its coupon code and validate it is currently usable.</summary>
        /// <remarks>
        /// Returns 404 if the code doesn't exist, or 400 if the code exists but is expired/inactive.
        /// On success, returns the full discount object so the client can display the discount name and value.
        /// </remarks>
        /// <param name="couponCode">The coupon code entered by the customer (case-insensitive)</param>
        /// <response code="200">Valid discount found</response>
        /// <response code="400">Code exists but is not currently valid (expired or inactive)</response>
        /// <response code="404">No discount found for that code</response>
        /// <response code="500">Database error</response>
        [HttpGet("coupon/{couponCode}")]
        [ProducesResponseType(typeof(DiscountDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>Record that a discount was applied — increments its usage counter by 1.</summary>
        /// <remarks>Call this after successfully applying a discount to a transaction so usage limits are enforced correctly.</remarks>
        /// <param name="discountId">The discount's unique identifier</param>
        /// <response code="200">Usage count incremented</response>
        /// <response code="404">Discount not found</response>
        /// <response code="500">Database error</response>
        [HttpPost("{discountId}/increment-usage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
