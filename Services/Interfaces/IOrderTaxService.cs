using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    public interface IOrderTaxService
    {
        Task<RemoveTaxesAndFeesResponse> RemoveTaxesAndFeesAsync(RemoveTaxesAndFeesRequest request);
    }
}
