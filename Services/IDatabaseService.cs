using PottaAPI.Models;

namespace PottaAPI.Services
{
    public interface IDatabaseService
    {
        Task TestConnectionAsync();

        // Sync & Statistics
        Task<SyncInfoDto> GetLastSyncInfoAsync();
        Task<DetailedSyncInfoDto> GetDetailedSyncInfoAsync();
        Task<DatabaseStatistics> GetDatabaseStatisticsAsync();
        Task<InventoryStatistics> GetInventoryStatisticsAsync();
        Task<TableStatistics> GetTableStatisticsAsync();
    }
}
