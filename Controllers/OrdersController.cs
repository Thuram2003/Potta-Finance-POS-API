using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public OrdersController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Create a new waiting transaction (mobile order)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponseDto<string>>> CreateOrder([FromBody] CreateWaitingTransactionDto order)
        {
            try
            {
                if (order.StaffId <= 0)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "StaffId is required",
                        Details = "A valid StaffId must be provided to create a transaction."
                    });
                }

                if (order.Items == null || !order.Items.Any())
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Order must contain at least one item",
                        Details = "Items list cannot be empty"
                    });
                }

                // Validate each item has required fields (like POS system)
                foreach (var item in order.Items)
                {
                    if (string.IsNullOrEmpty(item.ProductId) || string.IsNullOrEmpty(item.Name))
                    {
                        return BadRequest(new ErrorResponseDto
                        {
                            Error = "Invalid item data",
                            Details = "Each item must have ProductId and Name"
                        });
                    }

                    if (item.Quantity <= 0 || item.Price < 0)
                    {
                        return BadRequest(new ErrorResponseDto
                        {
                            Error = "Invalid item values",
                            Details = "Quantity must be greater than 0 and Price cannot be negative"
                        });
                    }

                    // Calculate total for each item (like CartItem does)
                    item.Total = item.SubTotal;
                }

                // Calculate total from items (like the POS system does)
                var totalAmount = order.Items.Sum(item => (double)item.SubTotal);
                if (totalAmount <= 0)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Invalid total amount",
                        Details = "Order total must be greater than zero"
                    });
                }

                var transactionId = await _databaseService.CreateWaitingTransactionAsync(order);

                return Ok(new ApiResponseDto<string>
                {
                    Success = true,
                    Message = $"Order created successfully with ID: {transactionId}",
                    Data = transactionId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to create order",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all waiting transactions (like POS system GetAllWaitingTransactions)
        /// </summary>
        [HttpGet("waiting")]
        public async Task<ActionResult<ApiResponseDto<List<WaitingTransactionDto>>>> GetWaitingTransactions([FromQuery] int? staffId = null)
        {
            try
            {
                var transactions = await _databaseService.GetWaitingTransactionsAsync(staffId);
                
                if (transactions == null)
                {
                    return Ok(new ApiResponseDto<List<WaitingTransactionDto>>
                    {
                        Success = true,
                        Message = "No waiting transactions found",
                        Data = new List<WaitingTransactionDto>()
                    });
                }

                return Ok(new ApiResponseDto<List<WaitingTransactionDto>>
                {
                    Success = true,
                    Message = $"Retrieved {transactions.Count} waiting transactions",
                    Data = transactions
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve waiting transactions",
                    Details = $"Database error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Update waiting transaction status
        /// </summary>
        [HttpPut("waiting/{transactionId}/status")]
        public async Task<ActionResult<ApiResponseDto<bool>>> UpdateTransactionStatus(
            string transactionId, 
            [FromBody] UpdateTransactionStatusDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Transaction ID is required",
                        Details = "TransactionId cannot be empty"
                    });
                }

                if (string.IsNullOrWhiteSpace(request?.Status))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Status is required",
                        Details = "Status field cannot be empty"
                    });
                }

                var success = await _databaseService.UpdateWaitingTransactionStatusAsync(transactionId, request.Status);

                if (!success)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Transaction not found",
                        Details = $"No waiting transaction found with ID: {transactionId}"
                    });
                }

                return Ok(new ApiResponseDto<bool>
                {
                    Success = true,
                    Message = $"Transaction {transactionId} updated successfully",
                    Data = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to update transaction status",
                    Details = $"Database error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Delete a waiting transaction (like POS system DeleteWaitingTransaction)
        /// </summary>
        [HttpDelete("waiting/{transactionId}")]
        public async Task<ActionResult<ApiResponseDto<bool>>> DeleteWaitingTransaction(string transactionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Transaction ID is required",
                        Details = "TransactionId cannot be empty"
                    });
                }

                var success = await _databaseService.DeleteWaitingTransactionAsync(transactionId);

                if (!success)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Transaction not found",
                        Details = $"No waiting transaction found with ID: {transactionId}"
                    });
                }

                return Ok(new ApiResponseDto<bool>
                {
                    Success = true,
                    Message = $"Transaction {transactionId} deleted successfully",
                    Data = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to delete transaction",
                    Details = $"Database error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get pending orders only
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponseDto<List<WaitingTransactionDto>>>> GetPendingOrders()
        {
            try
            {
                var allTransactions = await _databaseService.GetWaitingTransactionsAsync();
                var pendingOrders = allTransactions.Where(t => t.Status == "Pending").ToList();
                
                return Ok(new ApiResponseDto<List<WaitingTransactionDto>>
                {
                    Success = true,
                    Message = $"Retrieved {pendingOrders.Count} pending orders",
                    Data = pendingOrders
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve pending orders",
                    Details = ex.Message
                });
            }
        }
    }
}
