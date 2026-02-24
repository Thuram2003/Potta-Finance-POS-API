using System;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    // Staff authentication service (read-only, no CRUD)
    public class StaffService : IStaffService
    {
        private readonly string _connectionString;
        private const int CODE_EXPIRY_HOURS = 24;

        public StaffService(string connectionString)
        {
            _connectionString = connectionString;
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

                return new StaffLoginResponse
                {
                    Success = true,
                    Message = $"Welcome, {staff.FullName}!",
                    Staff = staff,
                    SessionToken = sessionToken
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
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, FirstName, LastName, Email, Phone, DailyCode, 
                           CodeGeneratedDate, IsActive
                    FROM Staff 
                    WHERE DailyCode = @DailyCode AND IsActive = 1";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@DailyCode", dailyCode);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var codeGeneratedDate = DateTime.Parse(reader["CodeGeneratedDate"].ToString()!);
                    var codeExpiresAt = codeGeneratedDate.AddHours(CODE_EXPIRY_HOURS);
                    var isExpired = DateTime.Now > codeExpiresAt;

                    return new StaffDTO
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        Phone = reader["Phone"]?.ToString() ?? "",
                        DailyCode = reader["DailyCode"]?.ToString() ?? "",
                        CodeGeneratedDate = codeGeneratedDate,
                        CodeExpiresAt = codeExpiresAt,
                        IsCodeExpired = isExpired,
                        IsActive = Convert.ToInt32(reader["IsActive"]) == 1
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff by code: {ex.Message}");
                return null;
            }
        }

        // Get staff info by ID
        public async Task<StaffDTO?> GetStaffByIdAsync(int staffId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, FirstName, LastName, Email, Phone, DailyCode, 
                           CodeGeneratedDate, IsActive
                    FROM Staff 
                    WHERE Id = @StaffId";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@StaffId", staffId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var codeGeneratedDate = reader["CodeGeneratedDate"] != DBNull.Value 
                        ? DateTime.Parse(reader["CodeGeneratedDate"].ToString()!) 
                        : DateTime.MinValue;
                    var codeExpiresAt = codeGeneratedDate != DateTime.MinValue 
                        ? codeGeneratedDate.AddHours(CODE_EXPIRY_HOURS) 
                        : DateTime.MinValue;
                    var isExpired = codeGeneratedDate != DateTime.MinValue && DateTime.Now > codeExpiresAt;

                    return new StaffDTO
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        Phone = reader["Phone"]?.ToString() ?? "",
                        DailyCode = reader["DailyCode"]?.ToString() ?? "",
                        CodeGeneratedDate = codeGeneratedDate,
                        CodeExpiresAt = codeExpiresAt,
                        IsCodeExpired = isExpired,
                        IsActive = Convert.ToInt32(reader["IsActive"]) == 1
                    };
                }

                return null;
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
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, FirstName, LastName, DailyCode, CodeGeneratedDate, IsActive
                    FROM Staff 
                    WHERE Id = @StaffId AND IsActive = 1";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@StaffId", staffId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var firstName = reader["FirstName"]?.ToString() ?? "";
                    var lastName = reader["LastName"]?.ToString() ?? "";
                    var dailyCode = reader["DailyCode"]?.ToString() ?? "";
                    var codeGeneratedDate = DateTime.Parse(reader["CodeGeneratedDate"].ToString()!);
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
    }
}
