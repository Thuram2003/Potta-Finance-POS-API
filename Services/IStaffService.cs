using System.Threading.Tasks;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    // Staff authentication service interface (read-only)
    public interface IStaffService
    {
        Task<StaffLoginResponse> LoginWithDailyCodeAsync(string dailyCode);
        Task<CodeValidationResponse> ValidateCodeAsync(string dailyCode);
        Task<StaffDTO?> GetStaffByCodeAsync(string dailyCode);
        Task<StaffQRCodeResponse> GetStaffQRCodeDataAsync(int staffId, string apiUrl);
    }
}
