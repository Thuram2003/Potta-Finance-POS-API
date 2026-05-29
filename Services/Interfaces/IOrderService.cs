using PottaAPI.Models;
using System.Collections.Generic;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for order/waiting transaction operations
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
        /// Update waiting transaction status (Pending → Ready → Completed)
        /// </summary>
        Task<bool> UpdateWaitingTransactionStatusAsync(string transactionId, string status);

        /// <summary>
        /// Delete a waiting transaction (order completed)
        /// </summary>
        Task<bool> DeleteWaitingTransactionAsync(string transactionId);

        /// <summary>
        /// Get orders for a specific table (for waiter apps)
        /// </summary>
        Task<List<WaitingTransactionDto>> GetOrdersByTableAsync(string tableId);

        /// <summary>
        /// Get orders for a specific customer (for customer history)
        /// </summary>
        Task<List<WaitingTransactionDto>> GetOrdersByCustomerAsync(string customerId);

        /// <summary>
        /// Update cart items for an existing waiting transaction
        /// </summary>
        Task<bool> UpdateWaitingTransactionItemsAsync(string transactionId, List<WaitingTransactionItemDto> items, int? staffId = null);

    }
}
