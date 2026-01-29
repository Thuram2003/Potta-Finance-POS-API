using PottaAPI.Models;

namespace PottaAPI.Services
{
    public interface IDatabaseService
    {
        Task TestConnectionAsync();

        // Staff & Authentication
        Task<List<StaffDto>> GetActiveStaffAsync();
        Task<StaffDto?> ValidateStaffCodeAsync(string dailyCode);

        // Sync & Statistics
        Task<SyncInfoDto> GetLastSyncInfoAsync();
        Task<DetailedSyncInfoDto> GetDetailedSyncInfoAsync();
        Task<DatabaseStatistics> GetDatabaseStatisticsAsync();
        Task<InventoryStatistics> GetInventoryStatisticsAsync();
        Task<TableStatistics> GetTableStatisticsAsync();
    }
}
