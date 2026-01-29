using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for order/waiting transaction operations
    /// Matches the WPF app's WaitingTransaction functionality
    /// </summary>
    public interface IOrderService
    {
        /// <summary>
        /// Create a new waiting transaction (mobile order)
        /// </summary>
        Task<string> CreateWaitingTransactionAsync(CreateWaitingTransactionDto transaction);

        /// <summary>
        /// Get all waiting transactions, optionally filtered by staff
        /// </summary>
        Task<List<WaitingTransactionDto>> GetWaitingTransactionsAsync(int? staffId = null);

        /// <summary>
        /// Get a specific waiting transaction by ID
        /// </summary>
        Task<WaitingTransactionDto?> GetWaitingTransactionByIdAsync(string transactionId);

        /// <summary>
        /// Update waiting transaction status
        /// </summary>
        Task<bool> UpdateWaitingTransactionStatusAsync(string transactionId, string status);

        /// <summary>
        /// Delete a waiting transaction
        /// </summary>
        Task<bool> DeleteWaitingTransactionAsync(string transactionId);

        /// <summary>
        /// Get pending orders only
        /// </summary>
        Task<List<WaitingTransactionDto>> GetPendingOrdersAsync();

        /// <summary>
        /// Get order statistics
        /// </summary>
        Task<OrderStatisticsDto> GetOrderStatisticsAsync();

        /// <summary>
        /// Get order summary by staff
        /// </summary>
        Task<List<StaffOrderSummaryDto>> GetStaffOrderSummaryAsync();

        /// <summary>
        /// Get order summary by table
        /// </summary>
        Task<List<TableOrderSummaryDto>> GetTableOrderSummaryAsync();

        /// <summary>
        /// Get orders for a specific table
        /// </summary>
        Task<List<WaitingTransactionDto>> GetOrdersByTableAsync(string tableId);

        /// <summary>
        /// Get orders for a specific customer
        /// </summary>
        Task<List<WaitingTransactionDto>> GetOrdersByCustomerAsync(string customerId);
    }
}
