using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for customer-related database operations
    /// </summary>
    public interface ICustomerService
    {
        /// <summary>
        /// Get all active customers
        /// </summary>
        Task<List<CustomerDto>> GetAllCustomersAsync();

        /// <summary>
        /// Get customer by ID
        /// </summary>
        Task<CustomerDto?> GetCustomerByIdAsync(string customerId);

        /// <summary>
        /// Search customers by name, email, or phone
        /// </summary>
        Task<CustomerSearchResponseDto> SearchCustomersAsync(CustomerSearchDto searchRequest);

        /// <summary>
        /// Get customer statistics
        /// </summary>
        Task<CustomerStatisticsDto> GetCustomerStatisticsAsync();
    }
}