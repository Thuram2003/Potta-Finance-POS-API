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
    /// Controller for mobile devices to view restaurant floor plans.
    /// Provides read-only access to floor plans and their positioned elements.
    /// Floor plan creation/editing/deletion is handled by desktop UI only.
    /// 
    /// Mobile workflow:
    /// 1. GET /api/floorplans - List all floor plans
    /// 2. GET /api/floorplans/{id} - Get floor plan with positioned elements
    /// 3. Use existing /api/tables endpoints for table details and status updates
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

        /// <summary>
        /// Get all active floor plans (list view)
        /// Mobile use case: Display floor plan selector/switcher
        /// </summary>
        /// <returns>List of floor plans with basic metadata</returns>
        /// <response code="200">Returns list of floor plans</response>
        /// <response code="500">Internal server error</response>
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

        /// <summary>
        /// Get specific floor plan with all positioned elements
        /// Mobile use case: Display floor plan with tables and elements for rendering
        /// 
        /// Returns:
        /// - Floor plan metadata (name, number, canvas dimensions, grid spacing)
        /// - All positioned elements with coordinates and dimensions
        /// - Table information for table elements (use /api/tables/{id} for full details)
        /// 
        /// Mobile developer should:
        /// - Use canvasWidth/canvasHeight for canvas size
        /// - Use gridSpacing (50px) for grid alignment
        /// - Position elements using xPosition, yPosition, width, height
        /// - Apply rotation if needed
        /// - Render elements in zIndex order (lower first)
        /// - For table elements, call /api/tables/{tableId} for full table details
        /// - For table elements, call /api/tables/{tableId}/seats for seat layout
        /// </summary>
        /// <param name="id">Floor plan ID</param>
        /// <returns>Floor plan with positioned elements</returns>
        /// <response code="200">Returns floor plan details</response>
        /// <response code="404">Floor plan not found</response>
        /// <response code="500">Internal server error</response>
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
