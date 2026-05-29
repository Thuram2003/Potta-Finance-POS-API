using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// View and update restaurant tables and seats from mobile devices.
    /// Table creation, editing, and deletion are desktop-only operations.
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

        /// <summary>Get all active tables with their current status.</summary>
        /// <remarks>Returns every table regardless of status (Available, Occupied, Reserved, Not Available).
        /// Use this to render the full floor plan on mobile.</remarks>
        /// <response code="200">List of tables</response>
        /// <response code="500">Database error</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<TableDTO>), 200)]
        [ProducesResponseType(500)]
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

        /// <summary>Get only tables with status <c>Available</c>.</summary>
        /// <remarks>Use this to populate a table picker when seating a new customer.</remarks>
        /// <response code="200">List of available tables</response>
        /// <response code="500">Database error</response>
        [HttpGet("available")]
        [ProducesResponseType(typeof(List<TableDTO>), 200)]
        [ProducesResponseType(500)]
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

        /// <summary>Get full details for a single table by its ID.</summary>
        /// <param name="tableId">The table's unique identifier</param>
        /// <response code="200">Table details including current status and seat count</response>
        /// <response code="404">Table not found</response>
        /// <response code="500">Database error</response>
        [HttpGet("{tableId}")]
        [ProducesResponseType(typeof(TableDTO), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
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

        /// <summary>Change a table's status (Available, Occupied, Reserved, Not Available).</summary>
        /// <remarks>
        /// Single endpoint for all table status transitions. Valid status values:
        /// - <c>Available</c> — table is free
        /// - <c>Occupied</c> — customers are seated
        /// - <c>Reserved</c> — table is booked
        /// - <c>Not Available</c> — table is out of service
        ///
        /// Sample request:
        ///
        ///     PATCH /api/tables/TBL-001/status
        ///     { "status": "Occupied", "updatedByStaffId": 3 }
        /// </remarks>
        /// <param name="tableId">The table's unique identifier</param>
        /// <response code="200">Status updated</response>
        /// <response code="400">Invalid status value or missing fields</response>
        /// <response code="404">Table not found</response>
        /// <response code="500">Database error</response>
        [HttpPatch("{tableId}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
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

        /// <summary>Get all seats for a specific table.</summary>
        /// <remarks>Returns each seat with its current status and any assigned customer info.
        /// Use this to render a seat picker before assigning a customer.</remarks>
        /// <param name="tableId">The table's unique identifier</param>
        /// <response code="200">List of seats for the table</response>
        /// <response code="500">Database error</response>
        [HttpGet("{tableId}/seats")]
        [ProducesResponseType(typeof(List<SeatDTO>), 200)]
        [ProducesResponseType(500)]
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

        /// <summary>Change a seat's status (Available, Occupied, Reserved, etc.).</summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PATCH /api/tables/seats/SEAT-001/status
        ///     { "status": "Occupied", "customerId": "CUST-001" }
        /// </remarks>
        /// <param name="seatId">The seat's unique identifier</param>
        /// <response code="200">Seat status updated</response>
        /// <response code="400">Invalid request body</response>
        /// <response code="404">Seat not found</response>
        /// <response code="500">Database error</response>
        [HttpPatch("seats/{seatId}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
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
