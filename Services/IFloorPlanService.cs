using System.Collections.Generic;
using System.Threading.Tasks;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Floor plan service interface for mobile device operations.
    /// Provides read-only access to floor plans and their positioned elements.
    /// Floor plan creation/editing/deletion is handled by desktop UI only.
    /// </summary>
    public interface IFloorPlanService
    {
        /// <summary>
        /// Get all active floor plans (list view)
        /// Mobile use case: Display floor plan selector
        /// </summary>
        Task<List<FloorPlanListDto>> GetAllFloorPlansAsync();

        /// <summary>
        /// Get specific floor plan with all positioned elements
        /// Mobile use case: Display floor plan with tables and elements
        /// </summary>
        Task<FloorPlanDetailDto?> GetFloorPlanByIdAsync(string floorPlanId);
    }
}
