using System;

namespace PottaAPI.Models
{
    /// <summary>
    /// Request model for staff login with daily code
    /// </summary>
    public class StaffLoginRequest
    {
        public string DailyCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for staff login
    /// </summary>
    public class StaffLoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public StaffDTO? Staff { get; set; }
        public string? SessionToken { get; set; }
    }

    /// <summary>
    /// Data transfer object for staff information (no sensitive data)
    /// </summary>
    public class StaffDTO
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string DailyCode { get; set; } = string.Empty;
        public DateTime CodeGeneratedDate { get; set; }
        public DateTime CodeExpiresAt { get; set; }
        public bool IsCodeExpired { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// QR code data structure for staff login
    /// </summary>
    public class StaffQRCodeData
    {
        public string Type { get; set; } = "staff_login";
        public string ApiUrl { get; set; } = string.Empty;
        public int StaffId { get; set; }
        public string DailyCode { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Response for QR code data request
    /// </summary>
    public class StaffQRCodeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public StaffQRCodeData? QRData { get; set; }
        public string? QRString { get; set; } // JSON string for QR generation
    }

    /// <summary>
    /// Response for code validation
    /// </summary>
    public class CodeValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsExpired { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
