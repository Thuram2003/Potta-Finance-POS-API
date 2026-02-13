using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Data;

namespace PottaAPI.Services
{
    /// <summary>
    /// Tax calculation service for PottaAPI
    /// Matches the desktop app's tax calculation logic exactly
    /// </summary>
    public class TaxService : ITaxService
    {
        private readonly string _connectionString;
        private readonly ILogger<TaxService> _logger;

        public TaxService(IConnectionStringProvider connectionStringProvider, ILogger<TaxService> logger)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
            _logger = logger;
        }

        /// <summary>
        /// Get tax configuration by ID
        /// </summary>
        public async Task<TaxDTO?> GetTaxByIdAsync(string taxId)
        {
            if (string.IsNullOrWhiteSpace(taxId))
            {
                return null;
            }

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT taxId, taxName, taxType, description, percentage, flatRate, 
                           percentageCap, isActive, createdDate, modifiedDate
                    FROM Taxes 
                    WHERE taxId = @taxId AND isActive = 1";
                command.Parameters.AddWithValue("@taxId", taxId);

                using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    _logger.LogWarning("Tax not found for ID: {TaxId}", taxId);
                    return null;
                }

                return MapTaxFromReader(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tax by ID: {TaxId}", taxId);
                return null;
            }
        }

        /// <summary>
        /// Get all active taxes
        /// </summary>
        public async Task<List<TaxDTO>> GetActiveTaxesAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT taxId, taxName, taxType, description, percentage, flatRate, 
                           percentageCap, isActive, createdDate, modifiedDate
                    FROM Taxes 
                    WHERE isActive = 1 
                    ORDER BY taxName";

                var taxes = new List<TaxDTO>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    taxes.Add(MapTaxFromReader(reader));
                }

                return taxes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active taxes");
                return new List<TaxDTO>();
            }
        }

        /// <summary>
        /// Calculate tax for a single waiting transaction item (cart item)
        /// Matches the desktop app's tax calculation logic exactly
        /// </summary>
        public decimal CalculateItemTax(WaitingTransactionItemDto item, TaxDTO? tax = null)
        {
            // If item is not taxable or has no tax ID, return 0
            if (!item.Taxable || string.IsNullOrWhiteSpace(item.TaxId))
            {
                item.TaxAmount = 0;
                return 0;
            }

            // If tax info not provided, we can't calculate (will be fetched in batch operations)
            if (tax == null)
            {
                return 0;
            }

            // Calculate taxable amount: SubTotal - Discount
            // SubTotal already includes modifiers: (Price * Quantity) - Discount
            decimal taxableAmount = item.SubTotal - item.Discount;

            if (taxableAmount <= 0)
            {
                item.TaxAmount = 0;
                return 0;
            }

            decimal taxAmount = 0;

            if (tax.TaxType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
            {
                // Calculate percentage-based tax
                taxAmount = taxableAmount * (decimal)(tax.Percentage / 100.0);

                // Apply cap if specified
                if (tax.PercentageCap.HasValue && taxAmount > (decimal)tax.PercentageCap.Value)
                {
                    taxAmount = (decimal)tax.PercentageCap.Value;
                }
            }
            else if (tax.TaxType.Equals("Flat Rate", StringComparison.OrdinalIgnoreCase))
            {
                // Flat rate is per item (multiplied by quantity)
                taxAmount = (decimal)tax.FlatRate * item.Quantity;
            }

            item.TaxAmount = Math.Round(taxAmount, 2);
            return item.TaxAmount;
        }

        /// <summary>
        /// Calculate total tax for all waiting transaction items
        /// </summary>
        public async Task<decimal> CalculateTotalTaxAsync(List<WaitingTransactionItemDto> items)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            try
            {
                // Get unique tax IDs from taxable items
                var taxIds = items
                    .Where(i => i.Taxable && !string.IsNullOrWhiteSpace(i.TaxId))
                    .Select(i => i.TaxId)
                    .Distinct()
                    .ToList();

                if (taxIds.Count == 0)
                {
                    return 0;
                }

                // Fetch all required taxes in one query
                var taxes = new Dictionary<string, TaxDTO>();
                foreach (var taxId in taxIds)
                {
                    var tax = await GetTaxByIdAsync(taxId);
                    if (tax != null)
                    {
                        taxes[taxId] = tax;
                    }
                }

                // Calculate tax for each item
                decimal totalTax = 0;
                foreach (var item in items)
                {
                    if (item.Taxable && !string.IsNullOrWhiteSpace(item.TaxId) && taxes.ContainsKey(item.TaxId))
                    {
                        decimal itemTax = CalculateItemTax(item, taxes[item.TaxId]);
                        totalTax += itemTax;
                    }
                    else
                    {
                        item.TaxAmount = 0;
                    }
                }

                return Math.Round(totalTax, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total tax");
                return 0;
            }
        }

        /// <summary>
        /// Get tax breakdown by tax type for waiting transaction items
        /// </summary>
        public async Task<List<TaxBreakdownDTO>> GetTaxBreakdownAsync(List<WaitingTransactionItemDto> items)
        {
            var breakdown = new Dictionary<string, TaxBreakdownDTO>();

            try
            {
                // Get unique tax IDs from taxable items
                var taxIds = items
                    .Where(i => i.Taxable && !string.IsNullOrWhiteSpace(i.TaxId))
                    .Select(i => i.TaxId)
                    .Distinct()
                    .ToList();

                if (taxIds.Count == 0)
                {
                    return new List<TaxBreakdownDTO>();
                }

                // Fetch all required taxes
                var taxes = new Dictionary<string, TaxDTO>();
                foreach (var taxId in taxIds)
                {
                    var tax = await GetTaxByIdAsync(taxId);
                    if (tax != null)
                    {
                        taxes[taxId] = tax;
                    }
                }

                // Group items by tax and calculate breakdown
                foreach (var item in items.Where(i => i.Taxable && !string.IsNullOrWhiteSpace(i.TaxId)))
                {
                    if (!taxes.ContainsKey(item.TaxId))
                    {
                        continue;
                    }

                    var tax = taxes[item.TaxId];
                    var taxKey = tax.TaxId;

                    if (!breakdown.ContainsKey(taxKey))
                    {
                        breakdown[taxKey] = new TaxBreakdownDTO
                        {
                            TaxName = tax.TaxName,
                            TaxType = tax.TaxType,
                            Rate = tax.TaxType.Equals("Percentage", StringComparison.OrdinalIgnoreCase) 
                                ? tax.Percentage 
                                : tax.FlatRate,
                            TaxableAmount = 0,
                            TaxAmount = 0,
                            DisplayText = tax.DisplayText
                        };
                    }

                    breakdown[taxKey].TaxableAmount += item.SubTotal - item.Discount;
                    breakdown[taxKey].TaxAmount += item.TaxAmount;
                }

                return breakdown.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tax breakdown");
                return new List<TaxBreakdownDTO>();
            }
        }

        /// <summary>
        /// Calculate complete order totals including tax for waiting transaction
        /// </summary>
        public async Task<TaxCalculationResult> CalculateOrderTotalsAsync(List<WaitingTransactionItemDto> items, decimal discount = 0)
        {
            try
            {
                // Calculate subtotal (includes modifiers)
                decimal subTotal = items.Sum(i => i.SubTotal);

                // Calculate total tax
                decimal totalTax = await CalculateTotalTaxAsync(items);

                // Calculate grand total
                decimal grandTotal = subTotal + totalTax - discount;

                // Get tax breakdown
                var taxBreakdown = await GetTaxBreakdownAsync(items);

                return new TaxCalculationResult
                {
                    SubTotal = Math.Round(subTotal, 2),
                    TotalTax = Math.Round(totalTax, 2),
                    GrandTotal = Math.Round(grandTotal, 2),
                    TaxBreakdown = taxBreakdown
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating order totals");
                return new TaxCalculationResult
                {
                    SubTotal = 0,
                    TotalTax = 0,
                    GrandTotal = 0,
                    TaxBreakdown = new List<TaxBreakdownDTO>()
                };
            }
        }

        /// <summary>
        /// Update tax amounts for all waiting transaction items
        /// </summary>
        public async Task UpdateOrderItemTaxesAsync(List<WaitingTransactionItemDto> items)
        {
            await CalculateTotalTaxAsync(items);
        }

        /// <summary>
        /// Map SqliteDataReader to TaxDTO
        /// </summary>
        private TaxDTO MapTaxFromReader(SqliteDataReader reader)
        {
            return new TaxDTO
            {
                TaxId = reader["taxId"]?.ToString() ?? string.Empty,
                TaxName = reader["taxName"]?.ToString() ?? string.Empty,
                TaxType = reader["taxType"]?.ToString() ?? string.Empty,
                Description = reader["description"]?.ToString() ?? string.Empty,
                Percentage = reader["percentage"] != DBNull.Value ? Convert.ToDouble(reader["percentage"]) : 0,
                FlatRate = reader["flatRate"] != DBNull.Value ? Convert.ToDouble(reader["flatRate"]) : 0,
                PercentageCap = reader["percentageCap"] != DBNull.Value 
                    ? Convert.ToDouble(reader["percentageCap"]) 
                    : null,
                IsActive = reader["isActive"] != DBNull.Value && Convert.ToBoolean(reader["isActive"]),
                CreatedDate = reader["createdDate"] != DBNull.Value 
                    ? Convert.ToDateTime(reader["createdDate"]) 
                    : DateTime.Now,
                ModifiedDate = reader["modifiedDate"] != DBNull.Value 
                    ? Convert.ToDateTime(reader["modifiedDate"]) 
                    : DateTime.Now
            };
        }
    }
}
