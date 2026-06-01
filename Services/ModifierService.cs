using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for modifier operations using Dapper
    /// </summary>
    public class ModifierService : IModifierService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache? _cache;

        private const string CacheKeyAllModifiers = "modifiers:all";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public ModifierService(
            IConnectionStringProvider connectionStringProvider,
            IMemoryCache? cache = null)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _cache = cache;
        }

        public async Task<List<ModifierDto>> GetAllModifiersAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllModifiers, out List<ModifierDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT m.modifierId, m.modifierName, m.priceChange, m.sortOrder, m.status, 
                       m.createdDate, m.modifiedDate, m.recipeId, m.useRecipePrice,
                       b.name as recipeName, b.cost as recipeCost
                FROM Modifiers m
                LEFT JOIN BundleItems b ON m.recipeId = b.bundleId
                WHERE m.status = 1
                ORDER BY m.sortOrder, m.modifierName";

            var modifiers = (await connection.QueryAsync<ModifierDto>(sql)).ToList();

            _cache?.Set(CacheKeyAllModifiers, modifiers, CacheDuration);
            return modifiers;
        }

        public async Task<ModifierDto?> GetModifierByIdAsync(string modifierId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT m.modifierId, m.modifierName, m.priceChange, m.sortOrder, m.status, 
                       m.createdDate, m.modifiedDate, m.recipeId, m.useRecipePrice,
                       b.name as recipeName, b.cost as recipeCost
                FROM Modifiers m
                LEFT JOIN BundleItems b ON m.recipeId = b.bundleId
                WHERE m.modifierId = @modifierId AND m.status = 1";

            return await connection.QueryFirstOrDefaultAsync<ModifierDto>(sql, new { modifierId });
        }
    }
}
