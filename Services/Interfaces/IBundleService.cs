using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for bundle and recipe operations
    /// </summary>
    public interface IBundleService
    {
        /// <summary>Get all active bundles</summary>
        Task<List<BundleDto>> GetAllBundlesAsync();

        /// <summary>Get bundle by ID with components and modifiers</summary>
        Task<BundleDto?> GetBundleByIdAsync(string bundleId);

        /// <summary>Get all recipes</summary>
        Task<List<BundleDto>> GetAllRecipesAsync();

        /// <summary>Get all modifiers associated with a specific bundle</summary>
        Task<List<ModifierDto>> GetBundleModifiersAsync(string bundleId);

        /// <summary>Get bundle components</summary>
        Task<List<BundleComponentDto>> GetBundleComponentsAsync(string bundleId);
    }
}
