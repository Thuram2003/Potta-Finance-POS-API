using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablesController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public TablesController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Get all active tables
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<TableDto>>>> GetTables()
        {
            try
            {
                var tables = await _databaseService.GetTablesAsync();
                return Ok(new ApiResponseDto<List<TableDto>>
                {
                    Success = true,
                    Message = $"Retrieved {tables.Count} tables",
                    Data = tables
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve tables",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Update table status (Available, Reserved, Not Available)
        /// </summary>
        [HttpPut("{tableId}/status")]
        public async Task<ActionResult<ApiResponseDto<bool>>> UpdateTableStatus(
            string tableId, 
            [FromBody] UpdateTableStatusDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Status is required",
                        Details = "Valid statuses: Available, Reserved, Not Available"
                    });
                }

                var validStatuses = new[] { "Available", "Reserved", "Not Available" };
                if (!validStatuses.Contains(request.Status))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Error = "Invalid status",
                        Details = $"Valid statuses: {string.Join(", ", validStatuses)}"
                    });
                }

                var success = await _databaseService.UpdateTableStatusAsync(
                    tableId, 
                    request.Status, 
                    request.CustomerId);

                if (!success)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Table not found",
                        Details = $"No table found with ID: {tableId}"
                    });
                }

                return Ok(new ApiResponseDto<bool>
                {
                    Success = true,
                    Message = $"Table status updated to {request.Status}",
                    Data = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to update table status",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get available tables only
        /// </summary>
        [HttpGet("available")]
        public async Task<ActionResult<ApiResponseDto<List<TableDto>>>> GetAvailableTables()
        {
            try
            {
                var allTables = await _databaseService.GetTablesAsync();
                var availableTables = allTables.Where(t => t.IsAvailable).ToList();
                
                return Ok(new ApiResponseDto<List<TableDto>>
                {
                    Success = true,
                    Message = $"Retrieved {availableTables.Count} available tables",
                    Data = availableTables
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve available tables",
                    Details = ex.Message
                });
            }
        }
    }
}
