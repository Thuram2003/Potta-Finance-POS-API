using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Restaurant operations endpoints
    /// Handles operations like adding notes, transferring servers, moving orders, etc.
    /// </summary>
    [ApiController]
    [Route("api/restaurant-operations")]
    [Produces("application/json")]
    public class RestaurantOperationsController : ControllerBase
    {
        private readonly IRestaurantOperationsService _restaurantOperationsService;
        private readonly ILogger<RestaurantOperationsController> _logger;

        public RestaurantOperationsController(
            IRestaurantOperationsService restaurantOperationsService,
            ILogger<RestaurantOperationsController> logger)
        {
            _restaurantOperationsService = restaurantOperationsService;
            _logger = logger;
        }

        /// <summary>
        /// Add notes to an order/transaction
        /// </summary>
        /// <param name="request">Add notes request</param>
        /// <returns>Add notes response</returns>
        /// <response code="200">Note added successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="404">Transaction not found</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/restaurant-operations/add-notes
        ///     {
        ///       "transactionId": "M20260219143022",
        ///       "noteText": "No onions, extra sauce",
        ///       "addedByStaffId": 1
        ///     }
        /// 
        /// Notes are appended to existing notes with line breaks.
        /// </remarks>
        [HttpPost("add-notes")]
        [ProducesResponseType(typeof(AddNotesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AddNotesResponse>> AddNotes([FromBody] AddNotesRequest request)
        {
            try
            {
                _logger.LogInformation("Adding notes to transaction {TransactionId}", request.TransactionId);
                
                var response = await _restaurantOperationsService.AddNotesAsync(request);
                
                _logger.LogInformation("Notes added successfully to transaction {TransactionId}", request.TransactionId);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Transaction not found: {TransactionId}", request.TransactionId);
                return NotFound(new ErrorResponseDto
                {
                    Error = "Transaction not found",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for transaction {TransactionId}", request.TransactionId);
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding notes to transaction {TransactionId}", request.TransactionId);
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred while adding notes"
                });
            }
        }

        /// <summary>
        /// Transfer an order to a different server/staff
        /// </summary>
        /// <param name="request">Transfer server request</param>
        /// <returns>Transfer server response</returns>
        /// <response code="200">Server transferred successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="404">Transaction or staff not found</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/restaurant-operations/transfer-server
        ///     {
        ///       "transactionId": "M20260219143022",
        ///       "newStaffId": 5,
        ///       "reason": "Shift change"
        ///     }
        /// 
        /// This updates both the transaction StaffId and all cart items' StaffId.
        /// Useful for shift changes or server rotation.
        /// </remarks>
        [HttpPost("transfer-server")]
        [ProducesResponseType(typeof(TransferServerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TransferServerResponse>> TransferServer([FromBody] TransferServerRequest request)
        {
            try
            {
                _logger.LogInformation("Transferring server for transaction {TransactionId} to staff {NewStaffId}", 
                    request.TransactionId, request.NewStaffId);
                
                var response = await _restaurantOperationsService.TransferServerAsync(request);
                
                _logger.LogInformation("Server transferred successfully for transaction {TransactionId}", 
                    request.TransactionId);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found for transaction {TransactionId}", request.TransactionId);
                return NotFound(new ErrorResponseDto
                {
                    Error = "Resource not found",
                    Details = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for transaction {TransactionId}", request.TransactionId);
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for transaction {TransactionId}", request.TransactionId);
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring server for transaction {TransactionId}", request.TransactionId);
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred while transferring server"
                });
            }
        }

        /// <summary>
        /// Transfer all orders from one staff to another (shift handover)
        /// </summary>
        /// <param name="request">Shift handover request</param>
        /// <returns>Shift handover response</returns>
        /// <response code="200">Shift handover completed successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="404">Staff not found</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/restaurant-operations/shift-handover
        ///     {
        ///       "currentStaffId": 3,
        ///       "newStaffId": 5,
        ///       "reason": "End of shift"
        ///     }
        /// 
        /// This transfers ALL pending orders from the current staff to the new staff.
        /// Useful for shift changes where all orders need to be handed over.
        /// After calling this endpoint, the desktop app should log out the current staff.
        /// </remarks>
        [HttpPost("shift-handover")]
        [ProducesResponseType(typeof(ShiftHandoverResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ShiftHandoverResponse>> ShiftHandover([FromBody] ShiftHandoverRequest request)
        {
            try
            {
                _logger.LogInformation("Shift handover: transferring all orders from staff {CurrentStaffId} to staff {NewStaffId}", 
                    request.CurrentStaffId, request.NewStaffId);
                
                var response = await _restaurantOperationsService.ShiftHandoverAsync(request);
                
                _logger.LogInformation("Shift handover completed: {OrdersTransferred} orders transferred", 
                    response.OrdersTransferred);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Staff not found during shift handover");
                return NotFound(new ErrorResponseDto
                {
                    Error = "Resource not found",
                    Details = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during shift handover");
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for shift handover");
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shift handover");
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred during shift handover"
                });
            }
        }

        /// <summary>
        /// Move an order from one table to another
        /// </summary>
        /// <param name="request">Move order request</param>
        /// <returns>Move order response</returns>
        /// <response code="200">Order moved successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="404">Transaction or table not found</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/restaurant-operations/move-order
        ///     {
        ///       "transactionId": "M20260219143022",
        ///       "targetTableId": "TBL005",
        ///       "reason": "Customer requested different table"
        ///     }
        /// 
        /// This moves an order from its current table to a new table.
        /// Updates both the transaction and table statuses.
        /// Source table becomes available, target table becomes occupied.
        /// </remarks>
        [HttpPost("move-order")]
        [ProducesResponseType(typeof(MoveOrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MoveOrderResponse>> MoveOrder([FromBody] MoveOrderRequest request)
        {
            try
            {
                _logger.LogInformation("Moving order {TransactionId} to table {TargetTableId}", 
                    request.TransactionId, request.TargetTableId);
                
                var response = await _restaurantOperationsService.MoveOrderAsync(request);
                
                _logger.LogInformation("Order moved successfully: {TransactionId} to {ToTableName}", 
                    request.TransactionId, response.ToTableName);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found while moving order");
                return NotFound(new ErrorResponseDto
                {
                    Error = "Resource not found",
                    Details = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while moving order");
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for moving order");
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving order");
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred while moving order"
                });
            }
        }

        /// <summary>
        /// Create a print bill request for desktop to process
        /// </summary>
        /// <param name="request">Print bill request details</param>
        /// <returns>Print bill request confirmation</returns>
        [HttpPost("print-bill")]
        [ProducesResponseType(typeof(PrintBillResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PrintBillResponse>> CreatePrintBillRequest([FromBody] PrintBillRequest request)
        {
            try
            {
                var response = await _restaurantOperationsService.CreatePrintBillRequestAsync(request);
                return CreatedAtAction(nameof(CreatePrintBillRequest), new { id = response.RequestId }, response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto
                {
                    Error = "Resource not found",
                    Details = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all pending print bill requests (for desktop polling)
        /// </summary>
        /// <returns>List of pending print bill requests</returns>
        [HttpGet("print-bill/pending")]
        [ProducesResponseType(typeof(List<PrintBillRequestDTO>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<PrintBillRequestDTO>>> GetPendingPrintBillRequests()
        {
            try
            {
                var requests = await _restaurantOperationsService.GetPendingPrintBillRequestsAsync();
                return Ok(requests);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Mark a print bill request as completed
        /// </summary>
        /// <param name="requestId">Request ID to complete</param>
        /// <param name="request">Completion details</param>
        /// <returns>Success status</returns>
        [HttpPut("print-bill/{requestId}/complete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompletePrintBillRequest(string requestId, [FromBody] CompletePrintBillRequest request)
        {
            try
            {
                var success = await _restaurantOperationsService.CompletePrintBillRequestAsync(requestId, request.CompletedBy);
                
                if (success)
                {
                    return Ok(new { message = "Print bill request completed successfully" });
                }
                else
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Resource not found",
                        Details = $"Print bill request {requestId} not found or already completed"
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Cancel a print bill request
        /// </summary>
        /// <param name="requestId">Request ID to cancel</param>
        /// <returns>Success status</returns>
        [HttpDelete("print-bill/{requestId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelPrintBillRequest(string requestId)
        {
            try
            {
                var success = await _restaurantOperationsService.CancelPrintBillRequestAsync(requestId);
                
                if (success)
                {
                    return Ok(new { message = "Print bill request cancelled successfully" });
                }
                else
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Resource not found",
                        Details = $"Print bill request {requestId} not found or already processed"
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        #region Pay Entire Bill Endpoints

        /// <summary>
        /// Create a pay entire bill request (mobile staff requests desktop to complete payment)
        /// </summary>
        /// <param name="request">Pay entire bill request</param>
        /// <returns>Pay entire bill response with request ID</returns>
        [HttpPost("pay-entire-bill")]
        [ProducesResponseType(typeof(PayEntireBillResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePayEntireBillRequest([FromBody] PayEntireBillRequest request)
        {
            try
            {
                var response = await _restaurantOperationsService.CreatePayEntireBillRequestAsync(request);
                return CreatedAtAction(
                    nameof(GetPendingPayEntireBillRequests),
                    new { id = response.RequestId },
                    response
                );
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all pending pay entire bill requests (desktop polls this)
        /// </summary>
        /// <returns>List of pending pay entire bill requests</returns>
        [HttpGet("pay-entire-bill/pending")]
        [ProducesResponseType(typeof(List<PayEntireBillRequestDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPendingPayEntireBillRequests()
        {
            var requests = await _restaurantOperationsService.GetPendingPayEntireBillRequestsAsync();
            return Ok(requests);
        }

        /// <summary>
        /// Complete a pay entire bill request (desktop marks as completed after payment)
        /// </summary>
        /// <param name="requestId">Request ID to complete</param>
        /// <param name="request">Completion details</param>
        /// <returns>Success status</returns>
        [HttpPut("pay-entire-bill/{requestId}/complete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompletePayEntireBillRequest(
            string requestId,
            [FromBody] CompletePayEntireBillRequest request)
        {
            try
            {
                var success = await _restaurantOperationsService.CompletePayEntireBillRequestAsync(
                    requestId,
                    request.CompletedBy
                );
                
                if (success)
                {
                    return Ok(new { message = "Pay entire bill request completed successfully" });
                }
                else
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Resource not found",
                        Details = $"Pay entire bill request {requestId} not found or already processed"
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Cancel a pay entire bill request
        /// </summary>
        /// <param name="requestId">Request ID to cancel</param>
        /// <returns>Success status</returns>
        [HttpDelete("pay-entire-bill/{requestId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelPayEntireBillRequest(string requestId)
        {
            try
            {
                var success = await _restaurantOperationsService.CancelPayEntireBillRequestAsync(requestId);
                
                if (success)
                {
                    return Ok(new { message = "Pay entire bill request cancelled successfully" });
                }
                else
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Resource not found",
                        Details = $"Pay entire bill request {requestId} not found or already processed"
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Refire To Kitchen Operations

        /// <summary>
        /// POST /api/restaurant-operations/refire-to-kitchen
        /// Mark an order as refired (updates WaitingTransaction)
        /// </summary>
        /// <param name="request">Refire request with transaction ID, staff ID, item indices, and reason</param>
        /// <returns>Refire response with confirmation</returns>
        [HttpPost("refire-to-kitchen")]
        [ProducesResponseType(typeof(RefireToKitchenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RefireToKitchenResponse>> RefireToKitchen([FromBody] RefireToKitchenRequest request)
        {
            try
            {
                _logger.LogInformation("Marking order as refired: {TransactionId} by staff {StaffId}", 
                    request.TransactionId, request.StaffId);
                
                var response = await _restaurantOperationsService.RefireToKitchenAsync(request);
                
                _logger.LogInformation("Order marked as refired successfully: {TransactionId} ({ItemsRefired} items)", 
                    request.TransactionId, response.ItemsRefired);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found for refire request: {TransactionId}", request.TransactionId);
                return NotFound(new ErrorResponseDto
                {
                    Error = "Resource not found",
                    Details = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for refire request: {TransactionId}", request.TransactionId);
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for refire: {TransactionId}", request.TransactionId);
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order as refired: {TransactionId}", request.TransactionId);
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred while marking order as refired"
                });
            }
        }

        #endregion

        #region Combine Orders Operations

        /// <summary>
        /// POST /api/restaurant-operations/combine-orders
        /// Combine multiple orders into one
        /// </summary>
        /// <param name="request">Combine orders request with transaction IDs, target table, and target staff</param>
        /// <returns>Combine orders response with new transaction ID</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/restaurant-operations/combine-orders
        ///     {
        ///       "transactionIds": ["M20260221001", "M20260221002", "M20260221003"],
        ///       "targetTableId": "TBL-001",
        ///       "targetStaffId": 5,
        ///       "notes": "Combined for birthday party"
        ///     }
        /// 
        /// This endpoint:
        /// - Combines 2+ waiting transactions into one
        /// - Merges duplicate items (same product, modifiers, price)
        /// - Sums quantities for merged items
        /// - Preserves items that can't be merged (different modifiers/prices)
        /// - Assigns combined order to target table and staff
        /// - Deletes original transactions after successful combination
        /// - Operation is atomic (all or nothing)
        /// 
        /// Orders can be combined from SAME table OR DIFFERENT tables.
        /// </remarks>
        [HttpPost("combine-orders")]
        [ProducesResponseType(typeof(CombineOrdersResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CombineOrdersResponse>> CombineOrders([FromBody] CombineOrdersRequest request)
        {
            try
            {
                _logger.LogInformation("Combining {Count} orders to table {TargetTableId}", 
                    request.TransactionIds.Count, request.TargetTableId);
                
                var response = await _restaurantOperationsService.CombineOrdersAsync(request);
                
                _logger.LogInformation("Orders combined successfully: {NewTransactionId} (merged {MergedItemsCount} items)", 
                    response.NewTransactionId, response.MergedItemsCount);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found for combine orders request");
                return NotFound(new ErrorResponseDto
                {
                    Error = "Resource not found",
                    Details = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for combine orders request");
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid operation",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for combine orders");
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error combining orders");
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred while combining orders"
                });
            }
        }

        #endregion

        #region Remove Taxes and Fees

        /// <summary>
        /// Remove taxes and fees from an order
        /// </summary>
        /// <param name="request">Remove taxes and fees request</param>
        /// <returns>Remove taxes and fees response</returns>
        /// <response code="200">Taxes and fees removed successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="404">Transaction not found</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/restaurant-operations/remove-taxes-and-fees
        ///     {
        ///       "transactionId": "M20260221001",
        ///       "staffId": 5,
        ///       "reason": "Tax-exempt organization"
        ///     }
        /// 
        /// This endpoint removes all taxes and fees from the order, setting the tax amount to 0.
        /// The operation is logged in the audit trail for compliance.
        /// </remarks>
        [HttpPost("remove-taxes-and-fees")]
        [ProducesResponseType(typeof(RemoveTaxesAndFeesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RemoveTaxesAndFeesResponse>> RemoveTaxesAndFees([FromBody] RemoveTaxesAndFeesRequest request)
        {
            try
            {
                _logger.LogInformation("Removing taxes and fees for transaction {TransactionId}", request.TransactionId);
                
                var response = await _restaurantOperationsService.RemoveTaxesAndFeesAsync(request);
                
                _logger.LogInformation("Taxes and fees removed successfully for transaction {TransactionId}", request.TransactionId);
                
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Transaction not found: {TransactionId}", request.TransactionId);
                return NotFound(new ErrorResponseDto
                {
                    Error = "Transaction not found",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for remove taxes and fees: {TransactionId}", request.TransactionId);
                return BadRequest(new ErrorResponseDto
                {
                    Error = "Invalid request",
                    Details = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing taxes and fees for transaction {TransactionId}", request.TransactionId);
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Internal server error",
                    Details = "An error occurred while removing taxes and fees"
                });
            }
        }

        #endregion
    }
}
