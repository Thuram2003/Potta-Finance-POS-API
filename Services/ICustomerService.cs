using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for customer-related database operations
    /// </summary>
    public interface ICustomerService
    {
        /// <summary>
        /// Get all active customers
        /// </summary>
        /// <returns>List of active customers</returns>
        Task<List<CustomerDto>> GetAllCustomersAsync();

        /// <summary>
        /// Get customer by ID
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>Customer if found, null otherwise</returns>
        Task<CustomerDto?> GetCustomerByIdAsync(string customerId);

        /// <summary>
        /// Search customers by name, email, or phone
        /// </summary>
        /// <param name="searchRequest">Search criteria</param>
        /// <returns>Search results with pagination</returns>
        Task<CustomerSearchResponseDto> SearchCustomersAsync(CustomerSearchDto searchRequest);

        /// <summary>
        /// Get customer statistics
        /// </summary>
        /// <returns>Customer statistics</returns>
        Task<CustomerStatisticsDto> GetCustomerStatisticsAsync();
    }
}