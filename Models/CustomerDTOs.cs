namespace PottaAPI.Models
{
    /// <summary>
    /// Customer data transfer object for API responses
    /// </summary>
    public class CustomerDto
    {
        public string CustomerId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string Country { get; set; } = "";
        public string ContactPerson { get; set; } = "";
        public string Gender { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public string CreditLimit { get; set; } = "";
        public string TaxId { get; set; } = "";
        public string Type { get; set; } = "";
        public decimal OpeningBalance { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public bool IsActive { get; set; }
        public string StatusDisplay => IsActive ? "Enabled" : "Disabled";
    }

    /// <summary>
    /// Customer search request DTO
    /// </summary>
    public class CustomerSearchDto
    {
        public string SearchTerm { get; set; } = "";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public bool IncludeInactive { get; set; } = false;
    }

    /// <summary>
    /// Customer search response DTO
    /// </summary>
    public class CustomerSearchResponseDto
    {
        public List<CustomerDto> Customers { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    /// <summary>
    /// Customer statistics DTO
    /// </summary>
    public class CustomerStatisticsDto
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int InactiveCustomers { get; set; }
        public int IndividualCustomers { get; set; }
        public int BusinessCustomers { get; set; }
        public int CustomersWithEmail { get; set; }
        public int CustomersWithPhone { get; set; }
        public decimal TotalOpeningBalance { get; set; }
        public decimal TotalCurrentBalance { get; set; }
        public DateTime? LastCustomerCreated { get; set; }
        public string MostCommonCountry { get; set; } = "";
        public string MostCommonCity { get; set; } = "";
    }
}