using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Staff authentication controller for mobile app
    /// READ-ONLY: No CRUD operations (staff management is desktop-only)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staffService;

        public StaffController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        /// <summary>
        /// Authenticate staff member using daily code
        /// POST /api/staff/login
        /// </summary>
        /// <param name="request">Login request with daily code</param>
        /// <returns>Login response with staff info and session token</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(StaffLoginResponse), 200)]
        [ProducesResponseType(typeof(StaffLoginResponse), 400)]
        public async Task<ActionResult<StaffLoginResponse>> Login([FromBody] StaffLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.DailyCode))
            {
                return BadRequest(new StaffLoginResponse
                {
                    Success = false,
                    Message = "Daily code is required"
                });
            }

            var response = await _staffService.LoginWithDailyCodeAsync(request.DailyCode);
            
            if (!response.Success)
            {
                return Ok(response); // Return 200 with success=false for invalid credentials
            }

            return Ok(response);
        }

        /// <summary>
        /// Validate if a daily code is valid and not expired
        /// GET /api/staff/validate/{code}
        /// </summary>
        /// <param name="code">4-digit daily code</param>
        /// <returns>Validation response</returns>
        [HttpGet("validate/{code}")]
        [ProducesResponseType(typeof(CodeValidationResponse), 200)]
        public async Task<ActionResult<CodeValidationResponse>> ValidateCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new CodeValidationResponse
                {
                    IsValid = false,
                    Message = "Daily code is required"
                });
            }

            var response = await _staffService.ValidateCodeAsync(code);
            return Ok(response);
        }

        /// <summary>
        /// Get QR code data for a specific staff member
        /// GET /api/staff/qr-data/{staffId}
        /// </summary>
        /// <param name="staffId">Staff ID</param>
        /// <returns>QR code data with JSON string</returns>
        [HttpGet("qr-data/{staffId}")]
        [ProducesResponseType(typeof(StaffQRCodeResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<StaffQRCodeResponse>> GetQRCodeData(int staffId)
        {
            // Get base URL from request
            var apiUrl = $"{Request.Scheme}://{Request.Host}/api";
            
            var response = await _staffService.GetStaffQRCodeDataAsync(staffId, apiUrl);
            
            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get staff information by daily code (without logging in)
        /// GET /api/staff/info/{code}
        /// </summary>
        /// <param name="code">4-digit daily code</param>
        /// <returns>Staff DTO or 404</returns>
        [HttpGet("info/{code}")]
        [ProducesResponseType(typeof(StaffDTO), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<StaffDTO>> GetStaffByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { message = "Daily code is required" });
            }

            var staff = await _staffService.GetStaffByCodeAsync(code);
            
            if (staff == null)
            {
                return NotFound(new { message = "Staff member not found" });
            }

            return Ok(staff);
        }
    }
}
