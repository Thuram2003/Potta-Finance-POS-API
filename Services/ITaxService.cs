using PottaAPI.Models;

namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for tax calculation service
    /// </summary>
    public interface ITaxService
    {
        /// <summary>
        /// Get tax configuration by ID
        /// </summary>
        Task<TaxDTO?> GetTaxByIdAsync(string taxId);

        /// <summary>
        /// Get all active taxes
        /// </summary>
        Task<List<TaxDTO>> GetActiveTaxesAsync();

        /// <summary>
        /// Calculate tax for a single waiting transaction item (cart item)
        /// </summary>
        decimal CalculateItemTax(WaitingTransactionItemDto item, TaxDTO? tax = null);

        /// <summary>
        /// Calculate total tax for all waiting transaction items
        /// </summary>
        Task<decimal> CalculateTotalTaxAsync(List<WaitingTransactionItemDto> items);

        /// <summary>
        /// Get tax breakdown by tax type for waiting transaction items
        /// </summary>
        Task<List<TaxBreakdownDTO>> GetTaxBreakdownAsync(List<WaitingTransactionItemDto> items);

        /// <summary>
        /// Calculate complete order totals including tax for waiting transaction
        /// </summary>
        Task<TaxCalculationResult> CalculateOrderTotalsAsync(List<WaitingTransactionItemDto> items, decimal discount = 0);

        /// <summary>
        /// Update tax amounts for all waiting transaction items
        /// </summary>
        Task UpdateOrderItemTaxesAsync(List<WaitingTransactionItemDto> items);
    }
}
