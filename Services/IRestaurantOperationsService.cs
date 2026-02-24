using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for restaurant operations service
    /// Handles operations like adding notes, transferring servers, moving orders, etc.
    /// </summary>
    public interface IRestaurantOperationsService
    {
        /// <summary>
        /// Add notes to an order/transaction
        /// </summary>
        /// <param name="request">Add notes request</param>
        /// <returns>Add notes response</returns>
        Task<AddNotesResponse> AddNotesAsync(AddNotesRequest request);
        
        /// <summary>
        /// Transfer an order to a different server/staff
        /// </summary>
        /// <param name="request">Transfer server request</param>
        /// <returns>Transfer server response</returns>
        Task<TransferServerResponse> TransferServerAsync(TransferServerRequest request);
        
        /// <summary>
        /// Transfer all orders from one staff to another (shift handover)
        /// </summary>
        /// <param name="request">Shift handover request</param>
        /// <returns>Shift handover response</returns>
        Task<ShiftHandoverResponse> ShiftHandoverAsync(ShiftHandoverRequest request);
        
        /// <summary>
        /// Move an order from one table to another
        /// </summary>
        /// <param name="request">Move order request</param>
        /// <returns>Move order response</returns>
        Task<MoveOrderResponse> MoveOrderAsync(MoveOrderRequest request);
        
        // Print Bill Operations
        Task<PrintBillResponse> CreatePrintBillRequestAsync(PrintBillRequest request);
        Task<List<PrintBillRequestDTO>> GetPendingPrintBillRequestsAsync();
        Task<bool> CompletePrintBillRequestAsync(string requestId, string? completedBy);
        Task<bool> CancelPrintBillRequestAsync(string requestId);
        
        // Pay Entire Bill Operations
        Task<PayEntireBillResponse> CreatePayEntireBillRequestAsync(PayEntireBillRequest request);
        Task<List<PayEntireBillRequestDTO>> GetPendingPayEntireBillRequestsAsync();
        Task<bool> CompletePayEntireBillRequestAsync(string requestId, string? completedBy);
        Task<bool> CancelPayEntireBillRequestAsync(string requestId);
        
        /// <summary>
        /// Mark an order as refired (updates WaitingTransaction)
        /// </summary>
        /// <param name="request">Refire to kitchen request</param>
        /// <returns>Refire to kitchen response</returns>
        Task<RefireToKitchenResponse> RefireToKitchenAsync(RefireToKitchenRequest request);
        
        /// <summary>
        /// Combine multiple orders into one
        /// </summary>
        /// <param name="request">Combine orders request</param>
        /// <returns>Combine orders response</returns>
        Task<CombineOrdersResponse> CombineOrdersAsync(CombineOrdersRequest request);
        
        /// <summary>
        /// Remove taxes and fees from an order
        /// </summary>
        /// <param name="request">Remove taxes and fees request</param>
        /// <returns>Remove taxes and fees response</returns>
        Task<RemoveTaxesAndFeesResponse> RemoveTaxesAndFeesAsync(RemoveTaxesAndFeesRequest request);
    }
}
