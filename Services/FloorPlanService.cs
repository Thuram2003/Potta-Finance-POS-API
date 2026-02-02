using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Floor plan service implementation for mobile device operations.
    /// Provides read-only access to floor plans and their positioned elements.
    /// </summary>
    public class FloorPlanService : IFloorPlanService
    {
        private readonly string _connectionString;

        public FloorPlanService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Floor Plan Operations

        public async Task<List<FloorPlanListDto>> GetAllFloorPlansAsync()
        {
            var floorPlans = new List<FloorPlanListDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    fp.floorPlanId, 
                    fp.floorName, 
                    fp.floorNumber, 
                    fp.isActive,
                    fp.createdDate, 
                    fp.modifiedDate,
                    COUNT(fpe.floorPlanElementId) as elementCount
                FROM FloorPlans fp
                LEFT JOIN FloorPlanElements fpe ON fp.floorPlanId = fpe.floorPlanId
                WHERE fp.isActive = 1
                GROUP BY fp.floorPlanId, fp.floorName, fp.floorNumber, fp.isActive, fp.createdDate, fp.modifiedDate
                ORDER BY fp.floorNumber ASC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                floorPlans.Add(new FloorPlanListDto
                {
                    FloorPlanId = reader["floorPlanId"]?.ToString() ?? "",
                    FloorName = reader["floorName"]?.ToString() ?? "",
                    FloorNumber = Convert.ToInt32(reader["floorNumber"]),
                    IsActive = Convert.ToBoolean(reader["isActive"]),
                    ElementCount = Convert.ToInt32(reader["elementCount"]),
                    CreatedDate = Convert.ToDateTime(reader["createdDate"]),
                    ModifiedDate = Convert.ToDateTime(reader["modifiedDate"])
                });
            }

            return floorPlans;
        }

        public async Task<FloorPlanDetailDto?> GetFloorPlanByIdAsync(string floorPlanId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get floor plan metadata
            var floorPlanCommand = connection.CreateCommand();
            floorPlanCommand.CommandText = @"
                SELECT floorPlanId, floorName, floorNumber, isActive, 
                       createdDate, modifiedDate
                FROM FloorPlans 
                WHERE floorPlanId = @floorPlanId";
            floorPlanCommand.Parameters.AddWithValue("@floorPlanId", floorPlanId);

            FloorPlanDetailDto? floorPlan = null;

            using (var reader = await floorPlanCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    floorPlan = new FloorPlanDetailDto
                    {
                        FloorPlanId = reader["floorPlanId"]?.ToString() ?? "",
                        FloorName = reader["floorName"]?.ToString() ?? "",
                        FloorNumber = Convert.ToInt32(reader["floorNumber"]),
                        IsActive = Convert.ToBoolean(reader["isActive"]),
                        CreatedDate = Convert.ToDateTime(reader["createdDate"]),
                        ModifiedDate = Convert.ToDateTime(reader["modifiedDate"]),
                        Elements = new List<FloorPlanElementDto>()
                    };
                }
            }

            if (floorPlan == null)
            {
                return null;
            }

            // Get all elements for this floor plan
            var elementsCommand = connection.CreateCommand();
            elementsCommand.CommandText = @"
                SELECT 
                    fpe.floorPlanElementId,
                    fpe.floorPlanId,
                    fpe.elementId,
                    fpe.tableId,
                    fpe.elementType,
                    fpe.xPosition,
                    fpe.yPosition,
                    fpe.width,
                    fpe.height,
                    fpe.rotation,
                    fpe.zIndex,
                    fpe.customColor,
                    fpe.customLabel,
                    fpe.isLocked,
                    fpe.createdDate,
                    fpe.modifiedDate,
                    t.tableName,
                    t.tableNumber,
                    t.capacity,
                    t.status as tableStatus,
                    t.size as tableSize,
                    t.shape as tableShape
                FROM FloorPlanElements fpe
                LEFT JOIN Tables t ON fpe.tableId = t.tableId AND t.isActive = 1
                WHERE fpe.floorPlanId = @floorPlanId
                ORDER BY fpe.zIndex, fpe.createdDate";
            elementsCommand.Parameters.AddWithValue("@floorPlanId", floorPlanId);

            using (var reader = await elementsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var element = new FloorPlanElementDto
                    {
                        FloorPlanElementId = reader["floorPlanElementId"]?.ToString() ?? "",
                        FloorPlanId = reader["floorPlanId"]?.ToString() ?? "",
                        ElementId = reader["elementId"] == DBNull.Value ? null : reader["elementId"]?.ToString(),
                        TableId = reader["tableId"] == DBNull.Value ? null : reader["tableId"]?.ToString(),
                        ElementType = reader["elementType"]?.ToString() ?? "Unknown",
                        XPosition = Convert.ToDouble(reader["xPosition"]),
                        YPosition = Convert.ToDouble(reader["yPosition"]),
                        Width = Convert.ToDouble(reader["width"]),
                        Height = Convert.ToDouble(reader["height"]),
                        Rotation = Convert.ToDouble(reader["rotation"]),
                        ZIndex = Convert.ToInt32(reader["zIndex"]),
                        CustomColor = reader["customColor"] == DBNull.Value ? null : reader["customColor"]?.ToString(),
                        CustomLabel = reader["customLabel"] == DBNull.Value ? null : reader["customLabel"]?.ToString(),
                        IsLocked = Convert.ToBoolean(reader["isLocked"]),
                        CreatedDate = Convert.ToDateTime(reader["createdDate"]),
                        ModifiedDate = Convert.ToDateTime(reader["modifiedDate"])
                    };

                    // Add table information if this element is a table
                    if (!string.IsNullOrEmpty(element.TableId))
                    {
                        element.TableInfo = new FloorPlanTableInfoDto
                        {
                            TableId = element.TableId,
                            TableName = reader["tableName"] == DBNull.Value ? null : reader["tableName"]?.ToString(),
                            TableNumber = reader["tableNumber"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tableNumber"]),
                            Capacity = reader["capacity"] == DBNull.Value ? 0 : Convert.ToInt32(reader["capacity"]),
                            Status = reader["tableStatus"] == DBNull.Value ? "Available" : reader["tableStatus"]?.ToString() ?? "Available",
                            Size = reader["tableSize"] == DBNull.Value ? null : reader["tableSize"]?.ToString(),
                            Shape = reader["tableShape"] == DBNull.Value ? null : reader["tableShape"]?.ToString()
                        };
                    }

                    floorPlan.Elements.Add(element);
                }
            }

            // Calculate canvas dimensions based on element positions
            if (floorPlan.Elements.Any())
            {
                var maxX = floorPlan.Elements.Max(e => e.XPosition + e.Width);
                var maxY = floorPlan.Elements.Max(e => e.YPosition + e.Height);

                // Use desktop default canvas size (1200x800) or calculated size, whichever is larger
                floorPlan.CanvasWidth = Math.Max(1200, maxX + 50); // Add 50px padding
                floorPlan.CanvasHeight = Math.Max(800, maxY + 50); // Add 50px padding
            }
            else
            {
                // Default canvas size when no elements
                floorPlan.CanvasWidth = 1200;
                floorPlan.CanvasHeight = 800;
            }

            // Set grid spacing (matches desktop implementation)
            floorPlan.GridSpacing = 50;

            return floorPlan;
        }

        #endregion
    }
}
