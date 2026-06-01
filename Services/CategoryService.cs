using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using PottaAPI.Models;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for category operations using Dapper
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache? _cache;

        private const string CacheKeyAllCategories = "categories:all";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public CategoryService(
            IConnectionStringProvider connectionStringProvider,
            IMemoryCache? cache = null)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _cache = cache;
        }

        public async Task<List<CategoryDto>> GetAllCategoriesAsync()
        {
            if (_cache != null && _cache.TryGetValue(CacheKeyAllCategories, out List<CategoryDto>? cached) && cached != null)
                return cached;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT c.categoryId, c.categoryName as name, c.description, c.isActive, c.createdDate,
                       (SELECT COUNT(*) FROM Products p WHERE p.categoryId = c.categoryId OR p.categories LIKE '%' || c.categoryId || '%') as itemCount
                FROM Categories c
                WHERE c.isActive = 1
                ORDER BY c.categoryName";

            var categories = (await connection.QueryAsync<CategoryDto>(sql)).ToList();

            _cache?.Set(CacheKeyAllCategories, categories, CacheDuration);
            return categories;
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(string categoryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT c.categoryId, c.categoryName as name, c.description, c.isActive, c.createdDate,
                       (SELECT COUNT(*) FROM Products p WHERE p.categoryId = c.categoryId OR p.categories LIKE '%' || c.categoryId || '%') as itemCount
                FROM Categories c
                WHERE c.categoryId = @categoryId AND c.isActive = 1";

            return await connection.QueryFirstOrDefaultAsync<CategoryDto>(sql, new { categoryId });
        }
    }
}
