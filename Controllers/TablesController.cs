using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Simplified controller for mobile devices to view and manage restaurant tables and seats.
    /// Table creation/editing/deletion is handled by desktop UI only.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TablesController : ControllerBase
    {
        private readonly ITableService _tableService;

        public TablesController(ITableService tableService)
        {
            _tableService = tableService;
        }

        /// <summary>
        /// Get all active tables with their current status
        /// Mobile use case: Display floor plan with all tables
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<TableDTO>), 200)]
        public async Task<ActionResult<List<TableDTO>>> GetAllTables()
        {
            try
            {
                var tables = await _tableService.GetAllTablesAsync();
                return Ok(tables);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tables", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all available tables (status = Available)
        /// Mobile use case: Filter tables for customer seating
        /// </summary>
        [HttpGet("available")]
        [ProducesResponseType(typeof(List<TableDTO>), 200)]
        public async Task<ActionResult<List<TableDTO>>> GetAvailableTables()
        {
            try
            {
                var tables = await _tableService.GetAvailableTablesAsync();
                return Ok(tables);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving available tables", error = ex.Message });
            }
        }

        /// <summary>
        /// Get specific table details by ID
        /// Mobile use case: View table details before selection
        /// </summary>
        [HttpGet("{tableId}")]
        [ProducesResponseType(typeof(TableDTO), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TableDTO>> GetTableById(string tableId)
        {
            try
            {
                var table = await _tableService.GetTableByIdAsync(tableId);
                if (table == null)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(table);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving table", error = ex.Message });
            }
        }

        /// <summary>
        /// Update table status (unified endpoint for all status changes)
        /// Mobile use case: Mark table as Occupied, Reserved, Available, etc.
        /// Replaces: /clear, /reserve, /not-available, /unpaid endpoints
        /// </summary>
        [HttpPatch("{tableId}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateTableStatus(string tableId, [FromBody] UpdateTableStatusDTO statusDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _tableService.UpdateTableStatusAsync(tableId, statusDto);
                if (!success)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(new { message = "Table status updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating table status", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all seats for a table
        /// Mobile use case: Display seat layout for customer assignment
        /// </summary>
        [HttpGet("{tableId}/seats")]
        [ProducesResponseType(typeof(List<SeatDTO>), 200)]
        public async Task<ActionResult<List<SeatDTO>>> GetTableSeats(string tableId)
        {
            try
            {
                var seats = await _tableService.GetTableSeatsAsync(tableId);
                return Ok(seats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving seats", error = ex.Message });
            }
        }

        /// <summary>
        /// Update seat status (unified endpoint for all seat status changes)
        /// Mobile use case: Mark seat as Occupied, Available, Reserved, etc.
        /// </summary>
        [HttpPatch("seats/{seatId}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateSeatStatus(string seatId, [FromBody] UpdateSeatStatusDTO statusDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _tableService.UpdateSeatStatusAsync(seatId, statusDto);
                if (!success)
                {
                    return NotFound(new { message = $"Seat with ID {seatId} not found" });
                }
                return Ok(new { message = "Seat status updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating seat status", error = ex.Message });
            }
        }
    }
}
