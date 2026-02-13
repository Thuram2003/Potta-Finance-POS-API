using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Controller for customer-related operations
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

        /// <summary>
        /// Get all active customers
        /// </summary>
        /// <returns>List of active customers</returns>
        [HttpGet]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { })]
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

        /// <summary>
        /// Get customer by ID
        /// </summary>
        /// <param name="id">Customer ID</param>
        /// <returns>Customer details if found</returns>
        [HttpGet("{id}")]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { "id" })]
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

        /// <summary>
        /// Search customers by name, email, or phone
        /// </summary>
        /// <param name="searchTerm">Search term (optional)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <param name="includeInactive">Include inactive customers (default: false)</param>
        /// <returns>Paginated search results</returns>
        [HttpGet("search")]
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

        /// <summary>
        /// Get customer statistics
        /// </summary>
        /// <returns>Customer statistics and insights</returns>
        [HttpGet("statistics")]
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