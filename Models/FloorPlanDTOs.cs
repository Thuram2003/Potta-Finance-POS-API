using System;
using System.Collections.Generic;

namespace PottaAPI.Models
{
    #region Floor Plan List DTOs

    /// <summary>
    /// DTO for floor plan list view
    /// Mobile use case: Display floor selector dropdown
    /// </summary>
    public class FloorPlanListDto
    {
        public string FloorPlanId { get; set; } = "";
        public string FloorName { get; set; } = "";
        public int FloorNumber { get; set; }
        public bool IsActive { get; set; }
        public int ElementCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Computed property for display
        public string DisplayName => $"{FloorName} (Floor {FloorNumber})";
    }

    #endregion

    #region Floor Plan Detail DTOs

    /// <summary>
    /// DTO for complete floor plan with elements
    /// Mobile use case: Render floor plan with positioned elements
    /// </summary>
    public class FloorPlanDetailDto
    {
        public string FloorPlanId { get; set; } = "";
        public string FloorName { get; set; } = "";
        public int FloorNumber { get; set; }
        public bool IsActive { get; set; }
        public double CanvasWidth { get; set; }
        public double CanvasHeight { get; set; }
        public int GridSpacing { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public List<FloorPlanElementDto> Elements { get; set; } = new List<FloorPlanElementDto>();

        // Computed property for display
        public string DisplayName => $"{FloorName} (Floor {FloorNumber})";
    }

    /// <summary>
    /// DTO for floor plan element (positioned element on canvas)
    /// Mobile use case: Position and render element at specific coordinates
    /// </summary>
    public class FloorPlanElementDto
    {
        public string FloorPlanElementId { get; set; } = "";
        public string FloorPlanId { get; set; } = "";
        public string? ElementId { get; set; }
        public string? TableId { get; set; }
        public string ElementType { get; set; } = "";
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public int ZIndex { get; set; }
        public string? CustomColor { get; set; }
        public string? CustomLabel { get; set; }
        public bool IsLocked { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Table information (if element is a table)
        public FloorPlanTableInfoDto? TableInfo { get; set; }

        // Computed properties
        public bool IsTable => !string.IsNullOrEmpty(TableId);
        public bool IsClickable => IsTable && !IsLocked;
    }

    /// <summary>
    /// DTO for basic table information in floor plan context
    /// Mobile use case: Display table status on floor plan
    /// For full table details, use GET /api/tables/{tableId}
    /// </summary>
    public class FloorPlanTableInfoDto
    {
        public string TableId { get; set; } = "";
        public string? TableName { get; set; }
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string Status { get; set; } = "Available";
        public string? Size { get; set; }
        public string? Shape { get; set; }

        // Computed properties
        public string DisplayName => !string.IsNullOrEmpty(TableName) ? TableName : $"Table {TableNumber}";
        public bool IsAvailable => Status == "Available";
        public bool IsOccupied => Status == "Occupied";
    }

    #endregion
}
