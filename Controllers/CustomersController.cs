using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Manage and look up customers. All operations are read-only — customer creation and editing
    /// are handled by the desktop app. Use these endpoints to populate customer pickers on mobile.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>Get all active customers.</summary>
        /// <remarks>Returns every customer with status = active. Results are cached for 30 seconds.</remarks>
        /// <response code="200">List of customers</response>
        /// <response code="500">Database error</response>
        [HttpGet]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { })]
        [ProducesResponseType(typeof(ApiResponseDto<List<CustomerDto>>), 200)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ApiResponseDto<List<CustomerDto>>>> GetAllCustomers()
        {
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();
                return Ok(new ApiResponseDto<List<CustomerDto>>
                {
                    Success = true,
                    Message = $"Retrieved {customers.Count} customers",
                    Data = customers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve customers",
                    Details = ex.Message
                });
            }
        }

        /// <summary>Get a single customer by their unique ID.</summary>
        /// <param name="id">The customer's unique identifier (e.g. <c>CUST-001</c>)</param>
        /// <response code="200">Customer found</response>
        /// <response code="404">No customer with that ID</response>
        /// <response code="500">Database error</response>
        [HttpGet("{id}")]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { "id" })]
        [ProducesResponseType(typeof(ApiResponseDto<CustomerDto>), 200)]
        [ProducesResponseType(typeof(ErrorResponseDto), 404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> GetCustomerById(string id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                
                if (customer == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Customer not found",
                        Details = $"No customer found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<CustomerDto>
                {
                    Success = true,
                    Message = "Customer retrieved successfully",
                    Data = customer
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve customer",
                    Details = ex.Message
                });
            }
        }

        /// <summary>Search customers by name, email, or phone number.</summary>
        /// <param name="searchTerm">Partial match against name, email, or phone. Leave empty to return all.</param>
        /// <param name="page">Page number (1-based, default 1)</param>
        /// <param name="pageSize">Results per page (1–100, default 50)</param>
        /// <param name="includeInactive">Set to <c>true</c> to include inactive customers (default false)</param>
        /// <response code="200">Paginated search results</response>
        /// <response code="500">Database error</response>
        [HttpGet("search")]
        [ProducesResponseType(typeof(ApiResponseDto<CustomerSearchResponseDto>), 200)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ApiResponseDto<CustomerSearchResponseDto>>> SearchCustomers(
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] bool includeInactive = false)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var searchRequest = new CustomerSearchDto
                {
                    SearchTerm = searchTerm ?? "",
                    Page = page,
                    PageSize = pageSize,
                    IncludeInactive = includeInactive
                };

                var results = await _customerService.SearchCustomersAsync(searchRequest);

                return Ok(new ApiResponseDto<CustomerSearchResponseDto>
                {
                    Success = true,
                    Message = $"Found {results.TotalCount} customers (showing page {page} of {results.TotalPages})",
                    Data = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to search customers",
                    Details = ex.Message
                });
            }
        }

        /// <summary>Get aggregate statistics about the customer base.</summary>
        /// <remarks>Returns totals such as active count, inactive count, and new customers this month.</remarks>
        /// <response code="200">Statistics object</response>
        /// <response code="500">Database error</response>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(ApiResponseDto<CustomerStatisticsDto>), 200)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ApiResponseDto<CustomerStatisticsDto>>> GetCustomerStatistics()
        {
            try
            {
                var statistics = await _customerService.GetCustomerStatisticsAsync();
                return Ok(new ApiResponseDto<CustomerStatisticsDto>
                {
                    Success = true,
                    Message = "Customer statistics retrieved successfully",
                    Data = statistics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve customer statistics",
                    Details = ex.Message
                });
            }
        }
    }
}