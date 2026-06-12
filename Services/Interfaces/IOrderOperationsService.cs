using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    public interface IOrderOperationsService
    {
        Task<AddNotesResponse> AddNotesAsync(AddNotesRequest request);
        Task<TransferServerResponse> TransferServerAsync(TransferServerRequest request);
        Task<ShiftHandoverResponse> ShiftHandoverAsync(ShiftHandoverRequest request);
        Task<MoveOrderResponse> MoveOrderAsync(MoveOrderRequest request);
        Task<RefireToKitchenResponse> RefireToKitchenAsync(RefireToKitchenRequest request);
        Task<CombineOrdersResponse> CombineOrdersAsync(CombineOrdersRequest request);
        Task<MobileCompletePaymentResponse> MobileCompletePaymentAsync(MobileCompletePaymentRequest request);
    }
}