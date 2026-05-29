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
    /// Read-only access to restaurant floor plans for mobile rendering.
    /// Floor plan creation, editing, and deletion are desktop-only.
    /// Typical mobile flow: list floor plans → pick one → render its elements → use /api/tables for live status.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class FloorPlansController : ControllerBase
    {
        private readonly IFloorPlanService _floorPlanService;

        public FloorPlansController(IFloorPlanService floorPlanService)
        {
            _floorPlanService = floorPlanService;
        }

        /// <summary>List all active floor plans (name and summary only).</summary>
        /// <remarks>Use this to show a floor plan selector. Call <c>GET /api/floorplans/{id}</c> to get the full layout.</remarks>
        /// <response code="200">List of floor plans</response>
        /// <response code="500">Database error</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<FloorPlanListDto>), 200)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<List<FloorPlanListDto>>> GetAllFloorPlans()
        {
            try
            {
                var floorPlans = await _floorPlanService.GetAllFloorPlansAsync();
                return Ok(floorPlans);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving floor plans", error = ex.Message });
            }
        }

        /// <summary>Get a floor plan with all its positioned elements (tables, walls, decorations, etc.).</summary>
        /// <remarks>
        /// Returns the canvas dimensions and every element with its position, size, rotation, and z-index.
        /// Use this to render the floor plan on mobile:
        ///
        /// - Set canvas to <c>canvasWidth × canvasHeight</c>
        /// - Snap elements to <c>gridSpacing</c> (50 px)
        /// - Position each element using <c>xPosition</c>, <c>yPosition</c>, <c>width</c>, <c>height</c>
        /// - Apply <c>rotation</c> if non-zero
        /// - Render in ascending <c>zIndex</c> order
        /// - For table elements, call <c>GET /api/tables/{tableId}</c> for live status
        /// - For seat layout, call <c>GET /api/tables/{tableId}/seats</c>
        /// </remarks>
        /// <param name="id">Floor plan unique identifier</param>
        /// <response code="200">Floor plan with all positioned elements</response>
        /// <response code="404">Floor plan not found</response>
        /// <response code="500">Database error</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(FloorPlanDetailDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<FloorPlanDetailDto>> GetFloorPlanById(string id)
        {
            try
            {
                var floorPlan = await _floorPlanService.GetFloorPlanByIdAsync(id);
                if (floorPlan == null)
                {
                    return NotFound(new { message = $"Floor plan with ID {id} not found" });
                }
                return Ok(floorPlan);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving floor plan", error = ex.Message });
            }
        }
    }
}
