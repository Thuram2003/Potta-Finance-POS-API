using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Controllers;

[ApiController]
[Route("api/restaurant-operations")]
[Produces("application/json")]
public class RestaurantOperationsController : ControllerBase
{
    private readonly IOrderOperationsService _orderOps;
    private readonly IBillRequestService _billRequests;
    private readonly IOrderTaxService _orderTax;
    private readonly ILogger<RestaurantOperationsController> _logger;

    public RestaurantOperationsController(
        IOrderOperationsService orderOps,
        IBillRequestService billRequests,
        IOrderTaxService orderTax,
        ILogger<RestaurantOperationsController> logger)
    {
        _orderOps = orderOps;
        _billRequests = billRequests;
        _orderTax = orderTax;
        _logger = logger;
    }

    // ── Order Operations ──

    /// <summary>
    /// Add notes to an order/transaction
    /// </summary>
    [HttpPost("add-notes")]
    public async Task<ActionResult<AddNotesResponse>> AddNotes([FromBody] AddNotesRequest request)
        => await HandleAsync(() => _orderOps.AddNotesAsync(request), request.TransactionId);

    /// <summary>
    /// Transfer an order to a different server/staff
    /// </summary>
    [HttpPost("transfer-server")]
    public async Task<ActionResult<TransferServerResponse>> TransferServer([FromBody] TransferServerRequest request)
        => await HandleAsync(() => _orderOps.TransferServerAsync(request), request.TransactionId);

    /// <summary>
    /// Transfer all orders from one staff to another (shift handover)
    /// </summary>
    [HttpPost("shift-handover")]
    public async Task<ActionResult<ShiftHandoverResponse>> ShiftHandover([FromBody] ShiftHandoverRequest request)
        => await HandleAsync(() => _orderOps.ShiftHandoverAsync(request));

    /// <summary>
    /// Move an order from one table to another
    /// </summary>
    [HttpPost("move-order")]
    public async Task<ActionResult<MoveOrderResponse>> MoveOrder([FromBody] MoveOrderRequest request)
        => await HandleAsync(() => _orderOps.MoveOrderAsync(request), request.TransactionId);

    /// <summary>
    /// Mark an order as refired to the kitchen (re-sends it for preparation)
    /// </summary>
    [HttpPost("refire-to-kitchen")]
    public async Task<ActionResult<RefireToKitchenResponse>> RefireToKitchen([FromBody] RefireToKitchenRequest request)
        => await HandleAsync(() => _orderOps.RefireToKitchenAsync(request), request.TransactionId);

    /// <summary>Combine two or more waiting transactions into a single order.</summary>
    [HttpPost("combine-orders")]
    public async Task<ActionResult<CombineOrdersResponse>> CombineOrders([FromBody] CombineOrdersRequest request)
        => await HandleAsync(() => _orderOps.CombineOrdersAsync(request));

    /// <summary>
    /// Create a print-bill request so the desktop app prints the bill for a transaction
    /// </summary>
    [HttpPost("print-bill")]
    public async Task<IActionResult> CreatePrintBillRequest([FromBody] PrintBillRequest request)
    {
        try
        {
            var response = await _billRequests.CreatePrintBillAsync(request);
            return CreatedAtAction(nameof(GetPendingPrintBillRequests), new { id = response.RequestId }, response);
        }
        catch (KeyNotFoundException ex) { return NotFound(new ErrorResponseDto { Error = "Not found", Details = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponseDto { Error = "Invalid operation", Details = ex.Message }); }
    }

    /// <summary>
    /// Get all pending print-bill requests (desktop polls this to know what to print)
    /// </summary>
    [HttpGet("print-bill/pending")]
    public async Task<ActionResult<List<PrintBillRequestDTO>>> GetPendingPrintBillRequests()
        => Ok(await _billRequests.GetPendingPrintBillsAsync());

    /// <summary>
    /// Mark a print-bill request as completed after the desktop has printed it
    /// </summary>
    [HttpPut("print-bill/{requestId}/complete")]
    public async Task<IActionResult> CompletePrintBillRequest(string requestId, [FromBody] CompletePrintBillRequest request)
        => await _billRequests.CompletePrintBillAsync(requestId, request.CompletedBy)
            ? Ok(new { message = "Completed" })
            : NotFound(new ErrorResponseDto { Error = "Not found", Details = $"Request {requestId} not found or already completed" });

    /// <summary>
    /// Cancel a pending print-bill request (e.g. customer changed their mind)
    /// </summary>
    [HttpDelete("print-bill/{requestId}")]
    public async Task<IActionResult> CancelPrintBillRequest(string requestId)
        => await _billRequests.CancelPrintBillAsync(requestId)
            ? Ok(new { message = "Cancelled" })
            : NotFound(new ErrorResponseDto { Error = "Not found", Details = $"Request {requestId} not found or already processed" });

    /// <summary>
    /// Create print-bill requests for every open order on a table at once
    /// </summary>
    [HttpPost("print-bill-by-table")]
    public async Task<IActionResult> CreatePrintBillByTableRequest([FromBody] PrintBillByTableRequest request)
    {
        try { return Ok(await _billRequests.CreatePrintBillByTableAsync(request)); }
        catch (KeyNotFoundException ex) { return NotFound(new ErrorResponseDto { Error = "Not found", Details = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponseDto { Error = "Invalid operation", Details = ex.Message }); }
    }

    /// <summary>
    /// Mobile staff requests the desktop to collect full payment for a transaction
    /// </summary>
    [HttpPost("pay-entire-bill")]
    public async Task<IActionResult> CreatePayEntireBillRequest([FromBody] PayEntireBillRequest request)
    {
        try
        {
            var response = await _billRequests.CreatePayEntireBillAsync(request);
            return CreatedAtAction(nameof(GetPendingPayEntireBillRequests), new { id = response.RequestId }, response);
        }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponseDto { Error = "Invalid operation", Details = ex.Message }); }
    }

    /// <summary>Get all pending pay-entire-bill requests (desktop polls this).</summary>
    [HttpGet("pay-entire-bill/pending")]
    public async Task<IActionResult> GetPendingPayEntireBillRequests()
        => Ok(await _billRequests.GetPendingPayBillsAsync());

    /// <summary>Mark a pay-entire-bill request as completed after the desktop has processed payment.</summary>
    [HttpPut("pay-entire-bill/{requestId}/complete")]
    public async Task<IActionResult> CompletePayEntireBillRequest(string requestId, [FromBody] CompletePayEntireBillRequest request)
        => await _billRequests.CompletePayBillAsync(requestId, request.CompletedBy)
            ? Ok(new { message = "Completed" })
            : NotFound(new ErrorResponseDto { Error = "Not found", Details = $"Request {requestId} not found or already processed" });

    /// <summary>Cancel a pending pay-entire-bill request.</summary>
    [HttpDelete("pay-entire-bill/{requestId}")]
    public async Task<IActionResult> CancelPayEntireBillRequest(string requestId)
        => await _billRequests.CancelPayBillAsync(requestId)
            ? Ok(new { message = "Cancelled" })
            : NotFound(new ErrorResponseDto { Error = "Not found", Details = $"Request {requestId} not found or already processed" });

    /// <summary>
    /// Mobile app completes payment directly (no desktop approval needed)
    /// Converts waiting transaction to completed transaction with payment info
    /// </summary>
    [HttpPost("mobile-complete-payment")]
    public async Task<ActionResult<MobileCompletePaymentResponse>> MobileCompletePayment([FromBody] MobileCompletePaymentRequest request)
        => await HandleAsync(() => _orderOps.MobileCompletePaymentAsync(request), request.TransactionId);

    /// <summary>
    /// Remove taxes from an order
    /// </summary>
    [HttpPost("remove-taxes-and-fees")]
    public async Task<ActionResult<RemoveTaxesAndFeesResponse>> RemoveTaxesAndFees([FromBody] RemoveTaxesAndFeesRequest request)
        => await HandleAsync(() => _orderTax.RemoveTaxesAndFeesAsync(request), request.TransactionId);

    // ── Helper ──
    private async Task<ActionResult<T>> HandleAsync<T>(Func<Task<T>> action, string? logId = null)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Not found: {Id}", logId);
            return NotFound(new ErrorResponseDto { Error = "Not found", Details = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Id}", logId);
            return BadRequest(new ErrorResponseDto { Error = "Invalid operation", Details = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument: {Id}", logId);
            return BadRequest(new ErrorResponseDto { Error = "Invalid request", Details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request: {Id}", logId);
            return StatusCode(500, new ErrorResponseDto { Error = "Internal server error", Details = "An error occurred" });
        }
    }
}