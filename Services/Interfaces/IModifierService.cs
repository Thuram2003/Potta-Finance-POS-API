using PottaAPI.Models;

namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for modifier operations
    /// </summary>
    public interface IModifierService
    {
        /// <summary>Get all active modifiers</summary>
        Task<List<ModifierDto>> GetAllModifiersAsync();

        /// <summary>Get modifier by ID</summary>
        Task<ModifierDto?> GetModifierByIdAsync(string modifierId);
    }
}
