using System;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using Dapper;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    // Staff authentication service (read-only, no CRUD)
    public class StaffService : IStaffService
    {
        private readonly string _connectionString;
        private const int CODE_EXPIRY_HOURS = 24;

        public StaffService(IConnectionStringProvider connectionStringProvider)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
        }

        // Authenticate staff with daily code
        public async Task<StaffLoginResponse> LoginWithDailyCodeAsync(string dailyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dailyCode))
                {
                    return new StaffLoginResponse
                    {
                        Success = false,
                        Message = "Daily code is required"
                    };
                }

                var staff = await GetStaffByCodeAsync(dailyCode);

                if (staff == null)
                {
                    return new StaffLoginResponse
                    {
                        Success = false,
                        Message = "Invalid daily code"
                    };
                }

                if (!staff.IsActive)
                {
                    return new StaffLoginResponse
                    {
                        Success = false,
                        Message = "Staff account is inactive"
                    };
                }

                if (staff.IsCodeExpired)
                {
                    return new StaffLoginResponse
                    {
                        Success = false,
                        Message = "Daily code has expired. Please request a new code."
                    };
                }

                // Generate session token (simple implementation - enhance for production)
                var sessionToken = GenerateSessionToken(staff.Id, dailyCode);

                // Get organization credentials for Instanvi token
                var (instanviToken, organizationId, branchId) = await GetOrganizationInfoAsync();

                return new StaffLoginResponse
                {
                    Success = true,
                    Message = $"Welcome, {staff.FullName}!",
                    Staff = staff,
                    SessionToken = sessionToken,
                    InstanviToken = instanviToken,
                    OrganizationId = organizationId,
                    BranchId = branchId
                };
            }
            catch (Exception ex)
            {
                return new StaffLoginResponse
                {
                    Success = false,
                    Message = $"Login failed: {ex.Message}"
                };
            }
        }

        // Validate daily code
        public async Task<CodeValidationResponse> ValidateCodeAsync(string dailyCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dailyCode))
                {
                    return new CodeValidationResponse
                    {
                        IsValid = false,
                        Message = "Daily code is required"
                    };
                }

                var staff = await GetStaffByCodeAsync(dailyCode);

                if (staff == null)
                {
                    return new CodeValidationResponse
                    {
                        IsValid = false,
                        Message = "Invalid daily code"
                    };
                }

                if (!staff.IsActive)
                {
                    return new CodeValidationResponse
                    {
                        IsValid = false,
                        Message = "Staff account is inactive"
                    };
                }

                if (staff.IsCodeExpired)
                {
                    return new CodeValidationResponse
                    {
                        IsValid = false,
                        IsExpired = true,
                        Message = "Daily code has expired",
                        ExpiresAt = staff.CodeExpiresAt
                    };
                }

                return new CodeValidationResponse
                {
                    IsValid = true,
                    IsExpired = false,
                    Message = "Code is valid",
                    ExpiresAt = staff.CodeExpiresAt
                };
            }
            catch (Exception ex)
            {
                return new CodeValidationResponse
                {
                    IsValid = false,
                    Message = $"Validation failed: {ex.Message}"
                };
            }
        }

        // Get staff info by daily code
        public async Task<StaffDTO?> GetStaffByCodeAsync(string dailyCode)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT Id, FirstName, LastName, Email, Phone, DailyCode, 
                           CodeGeneratedDate, IsActive
                    FROM Staff 
                    WHERE DailyCode = @DailyCode AND IsActive = 1";

                var staff = await connection.QueryFirstOrDefaultAsync<StaffDTO>(sql, new { DailyCode = dailyCode });

                if (staff != null)
                {
                    staff.CodeExpiresAt = staff.CodeGeneratedDate.AddHours(CODE_EXPIRY_HOURS);
                    staff.IsCodeExpired = DateTime.Now > staff.CodeExpiresAt;
                }

                return staff;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff by code: {ex.Message}");
                return null;
            }
        }

        // Get all active staff members (without daily codes for security)
        public async Task<List<StaffListDto>> GetAllActiveStaffAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT Id, FirstName, LastName, Email, Phone, IsActive
                    FROM Staff 
                    WHERE IsActive = 1
                    ORDER BY FirstName, LastName";

                var staff = await connection.QueryAsync<StaffListDto>(sql);
                return staff.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all staff: {ex.Message}");
                return new List<StaffListDto>();
            }
        }

        // Get staff info by ID
        public async Task<StaffDTO?> GetStaffByIdAsync(int staffId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT Id, FirstName, LastName, Email, Phone, DailyCode, 
                           CodeGeneratedDate, IsActive
                    FROM Staff 
                    WHERE Id = @StaffId";

                var staff = await connection.QueryFirstOrDefaultAsync<StaffDTO>(sql, new { StaffId = staffId });

                if (staff != null)
                {
                    if (staff.CodeGeneratedDate != DateTime.MinValue)
                    {
                        staff.CodeExpiresAt = staff.CodeGeneratedDate.AddHours(CODE_EXPIRY_HOURS);
                        staff.IsCodeExpired = DateTime.Now > staff.CodeExpiresAt;
                    }
                }

                return staff;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff by ID: {ex.Message}");
                return null;
            }
        }

        // Get QR code data for staff member
        public async Task<StaffQRCodeResponse> GetStaffQRCodeDataAsync(int staffId, string apiUrl)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT Id, FirstName, LastName, DailyCode, CodeGeneratedDate, IsActive
                    FROM Staff 
                    WHERE Id = @StaffId AND IsActive = 1";

                var staff = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { StaffId = staffId });

                if (staff != null)
                {
                    var firstName = (string)staff.FirstName ?? "";
                    var lastName = (string)staff.LastName ?? "";
                    var dailyCode = (string)staff.DailyCode ?? "";
                    
                    // Parse CodeGeneratedDate from string (SQLite stores DATETIME as TEXT)
                    DateTime codeGeneratedDate;
                    if (staff.CodeGeneratedDate is string dateString)
                    {
                        codeGeneratedDate = DateTime.Parse(dateString);
                    }
                    else
                    {
                        codeGeneratedDate = (DateTime)staff.CodeGeneratedDate;
                    }
                    
                    var expiresAt = codeGeneratedDate.AddHours(CODE_EXPIRY_HOURS);

                    var qrData = new StaffQRCodeData
                    {
                        Type = "staff_login",
                        ApiUrl = apiUrl,
                        StaffId = staffId,
                        DailyCode = dailyCode,
                        StaffName = $"{firstName} {lastName}".Trim(),
                        GeneratedAt = DateTime.Now,
                        ExpiresAt = expiresAt
                    };

                    var qrString = JsonSerializer.Serialize(qrData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return new StaffQRCodeResponse
                    {
                        Success = true,
                        Message = "QR code data generated successfully",
                        QRData = qrData,
                        QRString = qrString
                    };
                }

                return new StaffQRCodeResponse
                {
                    Success = false,
                    Message = "Staff member not found or inactive"
                };
            }
            catch (Exception ex)
            {
                return new StaffQRCodeResponse
                {
                    Success = false,
                    Message = $"Failed to generate QR code data: {ex.Message}"
                };
            }
        }

        // Generate session token
        private string GenerateSessionToken(int staffId, string dailyCode)
        {
            var timestamp = DateTime.Now.Ticks;
            var token = $"{staffId}:{dailyCode}:{timestamp}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(token));
        }

        // Get organization info for mobile app (Instanvi token, org ID, branch ID)
        private async Task<(string? instanviToken, string? organizationId, string? branchId)> GetOrganizationInfoAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var sql = @"
                    SELECT OrganizationId, AccessToken, BranchId
                    FROM OrganizationCredentials 
                    WHERE IsActive = 1 
                    LIMIT 1";

                var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql);

                if (result != null)
                {
                    string organizationId = result.OrganizationId;
                    string encryptedToken = result.AccessToken;
                    string branchId = result.BranchId;

                    // Decrypt the token (it's stored encrypted in the database)
                    string decryptedToken = DecryptToken(encryptedToken);

                    return (decryptedToken, organizationId, branchId);
                }

                return (null, null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting organization info: {ex.Message}");
                return (null, null, null);
            }
        }

        // Decrypt token (matches desktop's TokenStorageService)
        private string DecryptToken(string encryptedToken)
        {
            if (string.IsNullOrEmpty(encryptedToken))
                return null;

            try
            {
                // Simple Base64 decryption (matches desktop implementation)
                // In production, use proper encryption with a key
                var bytes = Convert.FromBase64String(encryptedToken);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // If decryption fails, return as-is (might already be decrypted)
                return encryptedToken;
            }
        }
    }
}
