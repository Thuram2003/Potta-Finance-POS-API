using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Controller for managing orders/waiting transactions
    /// Matches the WPF app's WaitingTransaction functionality
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
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

                var transactionId = await _orderService.CreateWaitingTransactionAsync(order);

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
                var transactions = await _orderService.GetWaitingTransactionsAsync(staffId);
                
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

                var success = await _orderService.UpdateWaitingTransactionStatusAsync(transactionId, request.Status);

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

                var success = await _orderService.DeleteWaitingTransactionAsync(transactionId);

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
                var allTransactions = await _orderService.GetWaitingTransactionsAsync();
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

        /// <summary>
        /// Get a specific waiting transaction by ID
        /// </summary>
        [HttpGet("waiting/{transactionId}")]
        public async Task<ActionResult<ApiResponseDto<WaitingTransactionDto>>> GetWaitingTransactionById(string transactionId)
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

                var transaction = await _orderService.GetWaitingTransactionByIdAsync(transactionId);

                if (transaction == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Transaction not found",
                        Details = $"No waiting transaction found with ID: {transactionId}"
                    });
                }

                return Ok(new ApiResponseDto<WaitingTransactionDto>
                {
                    Success = true,
                    Message = "Transaction retrieved successfully",
                    Data = transaction
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve transaction",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get order statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponseDto<OrderStatisticsDto>>> GetOrderStatistics()
        {
            try
            {
                var statistics = await _orderService.GetOrderStatisticsAsync();

                return Ok(new ApiResponseDto<OrderStatisticsDto>
                {
                    Success = true,
                    Message = "Order statistics retrieved successfully",
                    Data = statistics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve order statistics",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get order summary by staff
        /// </summary>
        [HttpGet("summary/staff")]
        public async Task<ActionResult<ApiResponseDto<List<StaffOrderSummaryDto>>>> GetStaffOrderSummary()
        {
            try
            {
                var summary = await _orderService.GetStaffOrderSummaryAsync();

                return Ok(new ApiResponseDto<List<StaffOrderSummaryDto>>
                {
                    Success = true,
                    Message = $"Retrieved order summary for {summary.Count} staff members",
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve staff order summary",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get order summary by table
        /// </summary>
        [HttpGet("summary/tables")]
        public async Task<ActionResult<ApiResponseDto<List<TableOrderSummaryDto>>>> GetTableOrderSummary()
        {
            try
            {
                var summary = await _orderService.GetTableOrderSummaryAsync();

                return Ok(new ApiResponseDto<List<TableOrderSummaryDto>>
                {
                    Success = true,
                    Message = $"Retrieved order summary for {summary.Count} tables",
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve table order summary",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get orders for a specific table
        /// </summary>
        [HttpGet("table/{tableId}")]
        public async Task<ActionResult<ApiResponseDto<List<WaitingTransactionDto>>>> GetOrdersByTable(string tableId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tableId))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Table ID is required",
                        Details = "TableId cannot be empty"
                    });
                }

                var orders = await _orderService.GetOrdersByTableAsync(tableId);

                return Ok(new ApiResponseDto<List<WaitingTransactionDto>>
                {
                    Success = true,
                    Message = $"Retrieved {orders.Count} orders for table",
                    Data = orders
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve orders by table",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get orders for a specific customer
        /// </summary>
        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponseDto<List<WaitingTransactionDto>>>> GetOrdersByCustomer(string customerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customerId))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Customer ID is required",
                        Details = "CustomerId cannot be empty"
                    });
                }

                var orders = await _orderService.GetOrdersByCustomerAsync(customerId);

                return Ok(new ApiResponseDto<List<WaitingTransactionDto>>
                {
                    Success = true,
                    Message = $"Retrieved {orders.Count} orders for customer",
                    Data = orders
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve orders by customer",
                    Details = ex.Message
                });
            }
        }
    }
}
