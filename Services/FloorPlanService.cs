using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using Dapper;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Floor plan service implementation for mobile device operations.
    /// Provides read-only access to floor plans and their positioned elements.
    /// </summary>
    public class FloorPlanService : IFloorPlanService
    {
        private readonly string _connectionString;

        public FloorPlanService(IConnectionStringProvider connectionStringProvider)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
        }

        #region Floor Plan Operations

        public async Task<List<FloorPlanListDto>> GetAllFloorPlansAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
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

            var floorPlans = await connection.QueryAsync<FloorPlanListDto>(sql);
            return floorPlans.ToList();
        }

        public async Task<FloorPlanDetailDto?> GetFloorPlanByIdAsync(string floorPlanId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get floor plan metadata
            var floorPlanSql = @"
                SELECT floorPlanId, floorName, floorNumber, isActive, 
                       createdDate, modifiedDate
                FROM FloorPlans 
                WHERE floorPlanId = @floorPlanId";

            var floorPlan = await connection.QueryFirstOrDefaultAsync<FloorPlanDetailDto>(floorPlanSql, new { floorPlanId });

            if (floorPlan == null)
            {
                return null;
            }

            // Get all elements for this floor plan
            // CAST to REAL to ensure proper double conversion from SQLite
            var elementsSql = @"
                SELECT 
                    fpe.floorPlanElementId,
                    fpe.floorPlanId,
                    fpe.elementId,
                    fpe.tableId,
                    fpe.elementType,
                    CAST(fpe.xPosition AS REAL) as xPosition,
                    CAST(fpe.yPosition AS REAL) as yPosition,
                    CAST(fpe.width AS REAL) as width,
                    CAST(fpe.height AS REAL) as height,
                    CAST(fpe.rotation AS REAL) as rotation,
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

            var elements = await connection.QueryAsync<FloorPlanElementDto, FloorPlanTableInfoDto?, FloorPlanElementDto>(
                elementsSql,
                (element, tableInfo) =>
                {
                    element.TableInfo = tableInfo;
                    return element;
                },
                new { floorPlanId },
                splitOn: "tableName"
            );

            floorPlan.Elements = elements.ToList();

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
