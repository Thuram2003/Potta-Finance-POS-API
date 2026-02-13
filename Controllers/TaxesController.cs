using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Tax calculation and management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TaxesController : ControllerBase
    {
        private readonly ITaxService _taxService;
        private readonly ILogger<TaxesController> _logger;

        public TaxesController(ITaxService taxService, ILogger<TaxesController> logger)
        {
            _taxService = taxService;
            _logger = logger;
        }

        /// <summary>
        /// Get all active taxes
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<TaxDTO>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TaxDTO>>> GetActiveTaxes()
        {
            try
            {
                var taxes = await _taxService.GetActiveTaxesAsync();
                _logger.LogInformation("Retrieved {Count} active taxes", taxes.Count);
                return Ok(taxes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active taxes");
                return StatusCode(500, new { error = "Failed to retrieve taxes", details = ex.Message });
            }
        }

        /// <summary>
        /// Get tax by ID
        /// </summary>
        [HttpGet("{taxId}")]
        [ProducesResponseType(typeof(TaxDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TaxDTO>> GetTaxById(string taxId)
        {
            try
            {
                var tax = await _taxService.GetTaxByIdAsync(taxId);
                
                if (tax == null)
                {
                    return NotFound(new { error = "Tax not found", taxId });
                }

                return Ok(tax);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tax by ID: {TaxId}", taxId);
                return StatusCode(500, new { error = "Failed to retrieve tax", details = ex.Message });
            }
        }

        /// <summary>
        /// Calculate tax for order items
        /// </summary>
        /// <remarks>
        /// Calculates taxes for a list of cart items. Each item should have:
        /// - productId: Product identifier
        /// - name: Product name
        /// - quantity: Number of items
        /// - price: Unit price
        /// - discount: Item-level discount (optional)
        /// - taxId: Tax configuration ID (if taxable)
        /// - taxable: Whether item is taxable
        /// - appliedModifiers: List of modifiers (optional)
        /// 
        /// The calculation includes:
        /// - SubTotal: Sum of (Price + Modifiers) × Quantity - Discount for all items
        /// - Tax: Calculated based on tax type (Percentage or Flat Rate)
        /// - GrandTotal: SubTotal + Tax
        /// - TaxBreakdown: Detailed breakdown by tax type
        /// 
        /// Example:
        /// Item: Burger, Price: 5000, Quantity: 2, Modifier: +500, Tax: 19.25%
        /// SubTotal: (5000 + 500) × 2 = 11,000
        /// Tax: 11,000 × 0.1925 = 2,117.50
        /// Total: 13,117.50
        /// </remarks>
        [HttpPost("calculate")]
        [ProducesResponseType(typeof(TaxCalculationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaxCalculationResult>> CalculateTax([FromBody] List<WaitingTransactionItemDto> items)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    return BadRequest(new { error = "Items list cannot be empty" });
                }

                var result = await _taxService.CalculateOrderTotalsAsync(items);
                
                _logger.LogInformation(
                    "Tax calculated - SubTotal: {SubTotal}, Tax: {Tax}, Total: {Total}", 
                    result.SubTotal, 
                    result.TotalTax, 
                    result.GrandTotal
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating tax");
                return StatusCode(500, new { error = "Failed to calculate tax", details = ex.Message });
            }
        }

        /// <summary>
        /// Get tax breakdown for order items
        /// </summary>
        /// <remarks>
        /// Returns detailed tax breakdown grouped by tax type.
        /// Useful for displaying on receipts or checkout screens.
        /// 
        /// Example response:
        /// [
        ///   {
        ///     "taxName": "VAT",
        ///     "taxType": "Percentage",
        ///     "rate": 19.25,
        ///     "taxableAmount": 11000,
        ///     "taxAmount": 2117.50,
        ///     "displayText": "19.25%",
        ///     "formattedDisplay": "VAT (19.25%): XAF 2,118"
        ///   }
        /// ]
        /// </remarks>
        [HttpPost("breakdown")]
        [ProducesResponseType(typeof(List<TaxBreakdownDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<TaxBreakdownDTO>>> GetTaxBreakdown([FromBody] List<WaitingTransactionItemDto> items)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    return BadRequest(new { error = "Items list cannot be empty" });
                }

                var breakdown = await _taxService.GetTaxBreakdownAsync(items);
                
                _logger.LogInformation("Tax breakdown calculated for {Count} items", items.Count);

                return Ok(breakdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tax breakdown");
                return StatusCode(500, new { error = "Failed to get tax breakdown", details = ex.Message });
            }
        }
    }
}
