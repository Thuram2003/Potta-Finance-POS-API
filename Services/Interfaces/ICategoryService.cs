using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for category operations
    /// </summary>
    public interface ICategoryService
    {
        /// <summary>Get all categories with item counts</summary>
        Task<List<CategoryDto>> GetAllCategoriesAsync();

        /// <summary>Get category by ID</summary>
        Task<CategoryDto?> GetCategoryByIdAsync(string categoryId);
    }
}
