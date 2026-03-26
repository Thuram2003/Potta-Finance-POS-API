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
        Task<AddNotesResponse> AddNotesAsync(AddNotesRequest request);
        
        /// <summary>
        /// Transfer an order to a different server/staff
        /// </summary>
        Task<TransferServerResponse> TransferServerAsync(TransferServerRequest request);
        
        /// <summary>
        /// Transfer all orders from one staff to another (shift handover)
        /// </summary>
        Task<ShiftHandoverResponse> ShiftHandoverAsync(ShiftHandoverRequest request);
        
        /// <summary>
        /// Move an order from one table to another
        /// </summary>
        Task<MoveOrderResponse> MoveOrderAsync(MoveOrderRequest request);
        
        // Print Bill Operations
        Task<PrintBillResponse> CreatePrintBillRequestAsync(PrintBillRequest request);
        Task<PrintBillByTableResponse> CreatePrintBillByTableRequestAsync(PrintBillByTableRequest request);
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
        Task<RefireToKitchenResponse> RefireToKitchenAsync(RefireToKitchenRequest request);
        
        /// <summary>
        /// Combine multiple orders into one
        /// </summary>
        Task<CombineOrdersResponse> CombineOrdersAsync(CombineOrdersRequest request);
        
        /// <summary>
        /// Remove taxes and fees from an order
        /// </summary>
        Task<RemoveTaxesAndFeesResponse> RemoveTaxesAndFeesAsync(RemoveTaxesAndFeesRequest request);
    }
}
