using System.Collections.Generic;
using System.Threading.Tasks;
using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    // Staff authentication service interface (read-only)
    public interface IStaffService
    {
        Task<StaffLoginResponse> LoginWithDailyCodeAsync(string dailyCode);
        Task<CodeValidationResponse> ValidateCodeAsync(string dailyCode);
        Task<StaffDTO?> GetStaffByCodeAsync(string dailyCode);
        Task<StaffDTO?> GetStaffByIdAsync(int staffId);
        Task<StaffQRCodeResponse> GetStaffQRCodeDataAsync(int staffId, string apiUrl);
        Task<List<StaffListDto>> GetAllActiveStaffAsync();
    }
}
