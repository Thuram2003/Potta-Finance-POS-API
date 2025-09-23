using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public StaffController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Get all active staff members
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<StaffDto>>>> GetActiveStaff()
        {
            try
            {
                var staff = await _databaseService.GetActiveStaffAsync();
                return Ok(new ApiResponseDto<List<StaffDto>>
                {
                    Success = true,
                    Message = $"Retrieved {staff.Count} active staff members",
                    Data = staff
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve staff",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Validate staff daily code for mobile app login
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<StaffLoginResponseDto>> ValidateStaffCode([FromBody] StaffLoginDto loginRequest)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(loginRequest.DailyCode))
                {
                    return BadRequest(new StaffLoginResponseDto
                    {
                        Success = false,
                        Message = "Daily code is required"
                    });
                }

                var staff = await _databaseService.ValidateStaffCodeAsync(loginRequest.DailyCode);
                
                if (staff == null)
                {
                    return Ok(new StaffLoginResponseDto
                    {
                        Success = false,
                        Message = "Invalid daily code"
                    });
                }

                // Check if code is expired (older than 24 hours)
                if (staff.IsCodeExpired)
                {
                    return Ok(new StaffLoginResponseDto
                    {
                        Success = false,
                        Message = "Daily code has expired. Please get a new code from the main POS system."
                    });
                }

                return Ok(new StaffLoginResponseDto
                {
                    Success = true,
                    Message = $"Welcome, {staff.FullName}!",
                    Staff = staff
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to validate staff code",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get staff daily codes (for sync purposes)
        /// </summary>
        [HttpGet("codes")]
        public async Task<ActionResult<ApiResponseDto<List<object>>>> GetStaffCodes()
        {
            try
            {
                var staff = await _databaseService.GetActiveStaffAsync();
                var codes = staff.Select(s => new 
                {
                    s.Id,
                    s.FullName,
                    s.DailyCode,
                    s.CodeGeneratedDate,
                    s.IsCodeExpired
                }).ToList();

                return Ok(new ApiResponseDto<List<object>>
                {
                    Success = true,
                    Message = $"Retrieved {codes.Count} staff codes",
                    Data = codes.Cast<object>().ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve staff codes",
                    Details = ex.Message
                });
            }
        }
    }
}
