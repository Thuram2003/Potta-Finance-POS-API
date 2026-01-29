using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Controller for managing restaurant tables and seats
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

        #region Table Endpoints

        /// <summary>
        /// Get all active tables
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
        /// Get all available tables
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
        /// Get table by ID
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
        /// Get table by table number
        /// </summary>
        [HttpGet("number/{tableNumber}")]
        [ProducesResponseType(typeof(TableDTO), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TableDTO>> GetTableByNumber(int tableNumber)
        {
            try
            {
                var table = await _tableService.GetTableByNumberAsync(tableNumber);
                if (table == null)
                {
                    return NotFound(new { message = $"Table number {tableNumber} not found" });
                }
                return Ok(table);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving table", error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new table
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(TableDTO), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<TableDTO>> CreateTable([FromBody] CreateTableDTO createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var table = await _tableService.CreateTableAsync(createDto);
                return CreatedAtAction(nameof(GetTableById), new { tableId = table.TableId }, table);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating table", error = ex.Message });
            }
        }

        /// <summary>
        /// Update table information
        /// </summary>
        [HttpPut("{tableId}")]
        [ProducesResponseType(typeof(TableDTO), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<TableDTO>> UpdateTable(string tableId, [FromBody] UpdateTableDTO updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var table = await _tableService.UpdateTableAsync(tableId, updateDto);
                return Ok(table);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating table", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete a table (soft delete)
        /// </summary>
        [HttpDelete("{tableId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteTable(string tableId)
        {
            try
            {
                var success = await _tableService.DeleteTableAsync(tableId);
                if (!success)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting table", error = ex.Message });
            }
        }

        #endregion

        #region Table Status Endpoints

        /// <summary>
        /// Update table status
        /// </summary>
        [HttpPatch("{tableId}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
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
        /// Clear table (set to Available)
        /// </summary>
        [HttpPost("{tableId}/clear")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ClearTable(string tableId)
        {
            try
            {
                var success = await _tableService.ClearTableAsync(tableId);
                if (!success)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(new { message = "Table cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clearing table", error = ex.Message });
            }
        }

        /// <summary>
        /// Reserve a table
        /// </summary>
        [HttpPost("{tableId}/reserve")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ReserveTable(string tableId, [FromBody] ReserveTableDTO reserveDto)
        {
            try
            {
                var success = await _tableService.ReserveTableAsync(tableId, reserveDto);
                if (!success)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(new { message = "Table reserved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error reserving table", error = ex.Message });
            }
        }

        /// <summary>
        /// Set table as not available
        /// </summary>
        [HttpPost("{tableId}/not-available")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SetTableNotAvailable(string tableId)
        {
            try
            {
                var success = await _tableService.SetTableNotAvailableAsync(tableId);
                if (!success)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(new { message = "Table set as not available" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating table status", error = ex.Message });
            }
        }

        /// <summary>
        /// Set table as unpaid
        /// </summary>
        [HttpPost("{tableId}/unpaid")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SetTableUnpaid(string tableId, [FromQuery] string? customerId = null, [FromQuery] string? transactionId = null)
        {
            try
            {
                var success = await _tableService.SetTableUnpaidAsync(tableId, customerId, transactionId);
                if (!success)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(new { message = "Table set as unpaid" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating table status", error = ex.Message });
            }
        }

        /// <summary>
        /// Check if table has pending transactions
        /// </summary>
        [HttpGet("{tableId}/has-pending")]
        [ProducesResponseType(typeof(bool), 200)]
        public async Task<ActionResult<bool>> HasPendingTransactions(string tableId)
        {
            try
            {
                var hasPending = await _tableService.HasPendingTransactionsAsync(tableId);
                return Ok(hasPending);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error checking pending transactions", error = ex.Message });
            }
        }

        /// <summary>
        /// Update table status based on pending transactions
        /// </summary>
        [HttpPost("{tableId}/update-status-from-transactions")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> UpdateTableStatusFromTransactions(string tableId)
        {
            try
            {
                await _tableService.UpdateTableStatusBasedOnTransactionsAsync(tableId);
                return Ok(new { message = "Table status updated based on transactions" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating table status", error = ex.Message });
            }
        }

        /// <summary>
        /// Update all tables status based on pending transactions
        /// </summary>
        [HttpPost("update-all-status-from-transactions")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> UpdateAllTablesStatusFromTransactions()
        {
            try
            {
                await _tableService.UpdateAllTablesStatusBasedOnTransactionsAsync();
                return Ok(new { message = "All table statuses updated based on transactions" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating table statuses", error = ex.Message });
            }
        }

        #endregion

        #region Seat Endpoints

        /// <summary>
        /// Get all seats for a table
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
        /// Get seat by ID
        /// </summary>
        [HttpGet("seats/{seatId}")]
        [ProducesResponseType(typeof(SeatDTO), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SeatDTO>> GetSeatById(string seatId)
        {
            try
            {
                var seat = await _tableService.GetSeatByIdAsync(seatId);
                if (seat == null)
                {
                    return NotFound(new { message = $"Seat with ID {seatId} not found" });
                }
                return Ok(seat);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving seat", error = ex.Message });
            }
        }

        /// <summary>
        /// Create seats for a table
        /// </summary>
        [HttpPost("seats")]
        [ProducesResponseType(typeof(List<SeatDTO>), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<SeatDTO>>> CreateSeats([FromBody] CreateSeatsDTO createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var seats = await _tableService.CreateSeatsForTableAsync(createDto);
                return CreatedAtAction(nameof(GetTableSeats), new { tableId = createDto.TableId }, seats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating seats", error = ex.Message });
            }
        }

        /// <summary>
        /// Update seat status
        /// </summary>
        [HttpPatch("seats/{seatId}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
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

        /// <summary>
        /// Select multiple seats (mark as occupied)
        /// </summary>
        [HttpPost("seats/select")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SelectSeats([FromBody] SelectSeatsDTO selectDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _tableService.SelectSeatsAsync(selectDto);
                return Ok(new { message = "Seats selected successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error selecting seats", error = ex.Message });
            }
        }

        /// <summary>
        /// Clear a seat (set to Available)
        /// </summary>
        [HttpPost("seats/{seatId}/clear")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ClearSeat(string seatId)
        {
            try
            {
                var success = await _tableService.ClearSeatAsync(seatId);
                if (!success)
                {
                    return NotFound(new { message = $"Seat with ID {seatId} not found" });
                }
                return Ok(new { message = "Seat cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clearing seat", error = ex.Message });
            }
        }

        /// <summary>
        /// Clear all seats for a table
        /// </summary>
        [HttpPost("{tableId}/seats/clear-all")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> ClearAllSeats(string tableId)
        {
            try
            {
                var success = await _tableService.ClearAllSeatsForTableAsync(tableId);
                return Ok(new { message = "All seats cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clearing seats", error = ex.Message });
            }
        }

        #endregion

        #region Combined Endpoints

        /// <summary>
        /// Get table with all its seats
        /// </summary>
        [HttpGet("{tableId}/with-seats")]
        [ProducesResponseType(typeof(TableWithSeatsDTO), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TableWithSeatsDTO>> GetTableWithSeats(string tableId)
        {
            try
            {
                var result = await _tableService.GetTableWithSeatsAsync(tableId);
                if (result == null)
                {
                    return NotFound(new { message = $"Table with ID {tableId} not found" });
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving table with seats", error = ex.Message });
            }
        }

        /// <summary>
        /// Get availability status for all tables
        /// </summary>
        [HttpGet("availability")]
        [ProducesResponseType(typeof(List<TableAvailabilityDTO>), 200)]
        public async Task<ActionResult<List<TableAvailabilityDTO>>> GetTablesAvailability()
        {
            try
            {
                var availability = await _tableService.GetTablesAvailabilityAsync();
                return Ok(availability);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving table availability", error = ex.Message });
            }
        }

        /// <summary>
        /// Get summary statistics for all tables
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(TableSummaryDTO), 200)]
        public async Task<ActionResult<TableSummaryDTO>> GetTableSummary()
        {
            try
            {
                var summary = await _tableService.GetTableSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving table summary", error = ex.Message });
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize default tables (for testing/setup)
        /// </summary>
        [HttpPost("initialize")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> InitializeDefaultTables()
        {
            try
            {
                await _tableService.InitializeDefaultTablesAsync();
                return Ok(new { message = "Default tables initialized successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error initializing tables", error = ex.Message });
            }
        }

        #endregion
    }
}
