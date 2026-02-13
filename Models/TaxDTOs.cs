namespace PottaAPI.Models
{
    /// <summary>
    /// DTO for tax configuration
    /// </summary>
    public class TaxDTO
    {
        public string TaxId { get; set; } = "";
        public string TaxName { get; set; } = "";
        public string TaxType { get; set; } = ""; // "Percentage" or "Flat Rate"
        public string Description { get; set; } = "";
        public double Percentage { get; set; } = 0;
        public double FlatRate { get; set; } = 0;
        public double? PercentageCap { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        /// <summary>
        /// Display text for receipts (e.g., "19.25%" or "XAF 500")
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (TaxType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{Percentage}%";
                }
                else if (TaxType.Equals("Flat Rate", StringComparison.OrdinalIgnoreCase))
                {
                    return $"XAF {FlatRate:N0}";
                }
                return "";
            }
        }
    }

    /// <summary>
    /// DTO for tax breakdown on receipts
    /// </summary>
    public class TaxBreakdownDTO
    {
        public string TaxName { get; set; } = "";
        public string TaxType { get; set; } = "";
        public double Rate { get; set; } = 0;
        public decimal TaxableAmount { get; set; } = 0;
        public decimal TaxAmount { get; set; } = 0;
        public string DisplayText { get; set; } = "";

        /// <summary>
        /// Formatted display for receipts
        /// </summary>
        public string FormattedDisplay => $"{TaxName} ({DisplayText}): XAF {TaxAmount:N0}";
    }

    /// <summary>
    /// DTO for complete tax calculation result
    /// </summary>
    public class TaxCalculationResult
    {
        public decimal SubTotal { get; set; } = 0;
        public decimal TotalTax { get; set; } = 0;
        public decimal GrandTotal { get; set; } = 0;
        public List<TaxBreakdownDTO> TaxBreakdown { get; set; } = new();

        /// <summary>
        /// Formatted subtotal
        /// </summary>
        public string FormattedSubTotal => $"XAF {SubTotal:N0}";

        /// <summary>
        /// Formatted total tax
        /// </summary>
        public string FormattedTotalTax => $"XAF {TotalTax:N0}";

        /// <summary>
        /// Formatted grand total
        /// </summary>
        public string FormattedGrandTotal => $"XAF {GrandTotal:N0}";
    }
}
