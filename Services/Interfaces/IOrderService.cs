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

        /// <summary>
        /// Get all online orders from the OnlineOrders table
        /// </summary>
        Task<List<OnlineOrderDto>> GetOnlineOrdersAsync(string? status = null);

        /// <summary>
        /// Get a specific online order by cloud ID or order number
        /// </summary>
        Task<OnlineOrderDto?> GetOnlineOrderByIdAsync(string idOrOrderNumber);

        /// <summary>
        /// Update online order status in the OnlineOrders table
        /// </summary>
        Task<bool> UpdateOnlineOrderStatusAsync(string idOrOrderNumber, string status, string? paymentMethod = null, string? note = null, string? orderStatus = null);
    }
}
