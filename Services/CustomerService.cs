using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using PottaAPI.Services;
using System.Data;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for customer-related database operations
    /// </summary>
    public class CustomerService : ICustomerService
    {
        private readonly string _connectionString;

        public CustomerService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<CustomerDto>> GetAllCustomersAsync()
        {
            var customers = new List<CustomerDto>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT customerId, firstName, lastName, gender, contactPerson, email, 
                       phone, date_of_birth, creditLimit, address, city, state, postalCode, 
                       country, taxId, type, openingBalance, createdDate, 
                       modifiedDate, isActive
                FROM Customer 
                WHERE isActive = 1
                ORDER BY firstName, lastName";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(CreateCustomerFromDataReader(reader));
            }

            return customers;
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(string customerId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT customerId, firstName, lastName, gender, contactPerson, email, 
                       phone, date_of_birth, creditLimit, address, city, state, postalCode, 
                       country, taxId, type, openingBalance, createdDate, 
                       modifiedDate, isActive
                FROM Customer 
                WHERE customerId = @customerId AND isActive = 1";
            command.Parameters.AddWithValue("@customerId", customerId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return CreateCustomerFromDataReader(reader);
            }

            return null;
        }

        public async Task<CustomerSearchResponseDto> SearchCustomersAsync(CustomerSearchDto searchRequest)
        {
            var customers = new List<CustomerDto>();
            var totalCount = 0;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Build the WHERE clause
            var whereClause = searchRequest.IncludeInactive ? "" : "WHERE isActive = 1";
            var searchClause = "";

            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                searchClause = @" AND (
                    firstName LIKE @search 
                    OR lastName LIKE @search 
                    OR email LIKE @search 
                    OR phone LIKE @search
                    OR contactPerson LIKE @search
                )";

                if (string.IsNullOrEmpty(whereClause))
                {
                    searchClause = searchClause.Replace("AND", "WHERE");
                }
            }

            // Get total count first
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM Customer {whereClause} {searchClause}";
            
            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                countCommand.Parameters.AddWithValue("@search", $"%{searchRequest.SearchTerm}%");
            }

            totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            // Get paginated results
            var offset = (searchRequest.Page - 1) * searchRequest.PageSize;
            var dataCommand = connection.CreateCommand();
            dataCommand.CommandText = $@"
                SELECT customerId, firstName, lastName, gender, contactPerson, email, 
                       phone, date_of_birth, creditLimit, address, city, state, postalCode, 
                       country, taxId, type, openingBalance, createdDate, 
                       modifiedDate, isActive
                FROM Customer 
                {whereClause} {searchClause}
                ORDER BY firstName, lastName
                LIMIT @pageSize OFFSET @offset";

            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                dataCommand.Parameters.AddWithValue("@search", $"%{searchRequest.SearchTerm}%");
            }
            dataCommand.Parameters.AddWithValue("@pageSize", searchRequest.PageSize);
            dataCommand.Parameters.AddWithValue("@offset", offset);

            using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(CreateCustomerFromDataReader(reader));
            }

            return new CustomerSearchResponseDto
            {
                Customers = customers,
                TotalCount = totalCount,
                Page = searchRequest.Page,
                PageSize = searchRequest.PageSize
            };
        }

        public async Task<CustomerStatisticsDto> GetCustomerStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var stats = new CustomerStatisticsDto();

            // Get basic counts
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(*) as TotalCustomers,
                    SUM(CASE WHEN isActive = 1 THEN 1 ELSE 0 END) as ActiveCustomers,
                    SUM(CASE WHEN isActive = 0 THEN 1 ELSE 0 END) as InactiveCustomers,
                    SUM(CASE WHEN LOWER(type) = 'individual' THEN 1 ELSE 0 END) as IndividualCustomers,
                    SUM(CASE WHEN LOWER(type) = 'business' THEN 1 ELSE 0 END) as BusinessCustomers,
                    SUM(CASE WHEN email IS NOT NULL AND email != '' THEN 1 ELSE 0 END) as CustomersWithEmail,
                    SUM(CASE WHEN phone IS NOT NULL AND phone != '' THEN 1 ELSE 0 END) as CustomersWithPhone,
                    COALESCE(SUM(openingBalance), 0) as TotalOpeningBalance,
                    MAX(createdDate) as LastCustomerCreated
                FROM Customer";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalCustomers = reader.IsDBNull("TotalCustomers") ? 0 : reader.GetInt32("TotalCustomers");
                stats.ActiveCustomers = reader.IsDBNull("ActiveCustomers") ? 0 : reader.GetInt32("ActiveCustomers");
                stats.InactiveCustomers = reader.IsDBNull("InactiveCustomers") ? 0 : reader.GetInt32("InactiveCustomers");
                stats.IndividualCustomers = reader.IsDBNull("IndividualCustomers") ? 0 : reader.GetInt32("IndividualCustomers");
                stats.BusinessCustomers = reader.IsDBNull("BusinessCustomers") ? 0 : reader.GetInt32("BusinessCustomers");
                stats.CustomersWithEmail = reader.IsDBNull("CustomersWithEmail") ? 0 : reader.GetInt32("CustomersWithEmail");
                stats.CustomersWithPhone = reader.IsDBNull("CustomersWithPhone") ? 0 : reader.GetInt32("CustomersWithPhone");
                stats.TotalOpeningBalance = reader.IsDBNull("TotalOpeningBalance") ? 0 : reader.GetDecimal("TotalOpeningBalance");
                
                if (!reader.IsDBNull("LastCustomerCreated"))
                {
                    stats.LastCustomerCreated = reader.GetDateTime("LastCustomerCreated");
                }
            }

            // Get most common country
            var countryCommand = connection.CreateCommand();
            countryCommand.CommandText = @"
                SELECT country, COUNT(*) as count 
                FROM Customer 
                WHERE country IS NOT NULL AND country != '' AND isActive = 1
                GROUP BY country 
                ORDER BY count DESC 
                LIMIT 1";

            using var countryReader = await countryCommand.ExecuteReaderAsync();
            if (await countryReader.ReadAsync())
            {
                stats.MostCommonCountry = countryReader["country"]?.ToString() ?? "";
            }

            // Get most common city
            var cityCommand = connection.CreateCommand();
            cityCommand.CommandText = @"
                SELECT city, COUNT(*) as count 
                FROM Customer 
                WHERE city IS NOT NULL AND city != '' AND isActive = 1
                GROUP BY city 
                ORDER BY count DESC 
                LIMIT 1";

            using var cityReader = await cityCommand.ExecuteReaderAsync();
            if (await cityReader.ReadAsync())
            {
                stats.MostCommonCity = cityReader["city"]?.ToString() ?? "";
            }

            // TODO: Calculate current balance from transactions
            stats.TotalCurrentBalance = stats.TotalOpeningBalance;

            return stats;
        }

        private CustomerDto CreateCustomerFromDataReader(SqliteDataReader reader)
        {
            var customer = new CustomerDto
            {
                CustomerId = reader["customerId"]?.ToString() ?? "",
                FirstName = reader["firstName"]?.ToString() ?? "",
                LastName = reader["lastName"]?.ToString() ?? "",
                Gender = reader["gender"]?.ToString() ?? "",
                ContactPerson = reader["contactPerson"]?.ToString() ?? "",
                Email = reader["email"]?.ToString() ?? "",
                Phone = reader["phone"]?.ToString() ?? "",
                CreditLimit = reader["creditLimit"]?.ToString() ?? "",
                Address = reader["address"]?.ToString() ?? "",
                City = reader["city"]?.ToString() ?? "",
                State = reader["state"]?.ToString() ?? "",
                PostalCode = reader["postalCode"]?.ToString() ?? "",
                TaxId = reader["taxId"]?.ToString() ?? "",
                Type = reader["type"]?.ToString() ?? "",
                OpeningBalance = reader.IsDBNull("openingBalance") ? 0 : reader.GetDecimal("openingBalance"),
                CreatedDate = reader.GetDateTime("createdDate"),
                ModifiedDate = reader.GetDateTime("modifiedDate"),
                IsActive = reader.GetBoolean("isActive"),
                Balance = 0 // TODO: Calculate from transactions if needed
            };

            // Handle DateOfBirth
            if (!reader.IsDBNull("date_of_birth") && !string.IsNullOrEmpty(reader["date_of_birth"].ToString()))
            {
                if (DateTime.TryParse(reader["date_of_birth"].ToString(), out DateTime dob))
                {
                    customer.DateOfBirth = dob;
                }
            }

            // Set country from database, but if it's empty, default to Cameroon
            var countryValue = reader["country"]?.ToString();
            customer.Country = string.IsNullOrWhiteSpace(countryValue) ? "Cameroon" : countryValue;

            return customer;
        }
    }
}