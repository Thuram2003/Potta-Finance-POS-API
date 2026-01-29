using System.Collections.Generic;
using System.Threading.Tasks;
using PottaAPI.Models;

namespace PottaAPI.Services
{
    public interface ITableService
    {
        // Table operations
        Task<List<TableDTO>> GetAllTablesAsync();
        Task<List<TableDTO>> GetAvailableTablesAsync();
        Task<TableDTO?> GetTableByIdAsync(string tableId);
        Task<TableDTO?> GetTableByNumberAsync(int tableNumber);
        Task<TableDTO> CreateTableAsync(CreateTableDTO createDto);
        Task<TableDTO> UpdateTableAsync(string tableId, UpdateTableDTO updateDto);
        Task<bool> DeleteTableAsync(string tableId);
        
        // Table status operations
        Task<bool> UpdateTableStatusAsync(string tableId, UpdateTableStatusDTO statusDto);
        Task<bool> ClearTableAsync(string tableId);
        Task<bool> ReserveTableAsync(string tableId, ReserveTableDTO reserveDto);
        Task<bool> SetTableNotAvailableAsync(string tableId);
        Task<bool> SetTableUnpaidAsync(string tableId, string? customerId = null, string? transactionId = null);
        
        // Table transaction operations
        Task<bool> HasPendingTransactionsAsync(string tableId);
        Task<bool> UpdateTableStatusBasedOnTransactionsAsync(string tableId);
        Task UpdateAllTablesStatusBasedOnTransactionsAsync();
        
        // Seat operations
        Task<List<SeatDTO>> GetTableSeatsAsync(string tableId);
        Task<SeatDTO?> GetSeatByIdAsync(string seatId);
        Task<List<SeatDTO>> CreateSeatsForTableAsync(CreateSeatsDTO createDto);
        Task<bool> UpdateSeatStatusAsync(string seatId, UpdateSeatStatusDTO statusDto);
        Task<bool> SelectSeatsAsync(SelectSeatsDTO selectDto);
        Task<bool> ClearSeatAsync(string seatId);
        Task<bool> ClearAllSeatsForTableAsync(string tableId);
        
        // Combined operations
        Task<TableWithSeatsDTO?> GetTableWithSeatsAsync(string tableId);
        Task<List<TableAvailabilityDTO>> GetTablesAvailabilityAsync();
        Task<TableSummaryDTO> GetTableSummaryAsync();
        
        // Initialization
        Task InitializeDefaultTablesAsync();
    }
}
