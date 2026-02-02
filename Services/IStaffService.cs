using System.Threading.Tasks;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for staff authentication and validation services
    /// READ-ONLY: No CRUD operations (staff management is desktop-only)
    /// </summary>
    public interface IStaffService
    {
        /// <summary>
        /// Authenticate staff member using daily code
        /// </summary>
        /// <param name="dailyCode">4-digit daily code</param>
        /// <returns>Login response with staff info and session token</returns>
        Task<StaffLoginResponse> LoginWithDailyCodeAsync(string dailyCode);

        /// <summary>
        /// Validate if a daily code is valid and not expired
        /// </summary>
        /// <param name="dailyCode">4-digit daily code</param>
        /// <returns>Validation response with expiry info</returns>
        Task<CodeValidationResponse> ValidateCodeAsync(string dailyCode);

        /// <summary>
        /// Get staff information by daily code (without logging in)
        /// </summary>
        /// <param name="dailyCode">4-digit daily code</param>
        /// <returns>Staff DTO or null if not found</returns>
        Task<StaffDTO?> GetStaffByCodeAsync(string dailyCode);

        /// <summary>
        /// Get QR code data for a specific staff member
        /// </summary>
        /// <param name="staffId">Staff ID</param>
        /// <param name="apiUrl">Base API URL for mobile app</param>
        /// <returns>QR code data response</returns>
        Task<StaffQRCodeResponse> GetStaffQRCodeDataAsync(int staffId, string apiUrl);
    }
}
