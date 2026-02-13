using System.Collections.Generic;
using System.Threading.Tasks;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    // Table service interface for mobile operations
    public interface ITableService
    {
        // Core table operations (6 essential methods for mobile)
        Task<List<TableDTO>> GetAllTablesAsync();
        Task<List<TableDTO>> GetAvailableTablesAsync();
        Task<TableDTO?> GetTableByIdAsync(string tableId);
        Task<bool> UpdateTableStatusAsync(string tableId, UpdateTableStatusDTO statusDto);
        Task<List<SeatDTO>> GetTableSeatsAsync(string tableId);
        Task<bool> UpdateSeatStatusAsync(string seatId, UpdateSeatStatusDTO statusDto);
    }
}
