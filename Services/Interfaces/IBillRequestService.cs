using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    public interface IBillRequestService
    {
        // Print Bill
        Task<PrintBillResponse> CreatePrintBillAsync(PrintBillRequest request);
        Task<PrintBillByTableResponse> CreatePrintBillByTableAsync(PrintBillByTableRequest request);
        Task<List<PrintBillRequestDTO>> GetPendingPrintBillsAsync();
        Task<bool> CompletePrintBillAsync(string requestId, string? completedBy);
        Task<bool> CancelPrintBillAsync(string requestId);

        // Pay Entire Bill
        Task<PayEntireBillResponse> CreatePayEntireBillAsync(PayEntireBillRequest request);
        Task<List<PayEntireBillRequestDTO>> GetPendingPayBillsAsync();
        Task<bool> CompletePayBillAsync(string requestId, string? completedBy);
        Task<bool> CancelPayBillAsync(string requestId);
    }
}