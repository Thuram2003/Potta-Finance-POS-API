using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public CustomersController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Get all active customers
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<CustomerDto>>>> GetCustomers()
        {
            try
            {
                var customers = await _databaseService.GetCustomersAsync();
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
        [HttpGet("{customerId}")]
        public async Task<ActionResult<ApiResponseDto<CustomerDto>>> GetCustomerById(string customerId)
        {
            try
            {
                var customer = await _databaseService.GetCustomerByIdAsync(customerId);
                
                if (customer == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Customer not found",
                        Details = $"No customer found with ID: {customerId}"
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
    }
}
