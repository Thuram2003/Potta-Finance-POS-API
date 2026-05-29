using Microsoft.Data.Sqlite;
using PottaAPI.Models;
using System.Data;
using Dapper;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Services
{
    /// <summary>
    /// Service for customer-related database operations
    /// </summary>
    public class CustomerService : ICustomerService
    {
        private readonly string _connectionString;

        public CustomerService(IConnectionStringProvider connectionStringProvider)
        {
            _connectionString = connectionStringProvider.GetConnectionString();
        }

        public async Task<List<CustomerDto>> GetAllCustomersAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT customerId, firstName, lastName, gender, contactPerson, email, 
                       phone, date_of_birth, creditLimit, address, city, state, postalCode, 
                       country, taxId, type, openingBalance, createdDate, 
                       modifiedDate, isActive
                FROM Customer 
                WHERE isActive = 1
                ORDER BY firstName, lastName";

            var customers = await connection.QueryAsync<CustomerDto>(sql);
            
            // Post-process to handle defaults
            foreach (var customer in customers)
            {
                if (string.IsNullOrWhiteSpace(customer.Country))
                    customer.Country = "Cameroon";
            }
            
            return customers.ToList();
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(string customerId)
        {
            using var connection = new SqliteConnection(_connectionString);
            
            var sql = @"
                SELECT customerId, firstName, lastName, gender, contactPerson, email, 
                       phone, date_of_birth, creditLimit, address, city, state, postalCode, 
                       country, taxId, type, openingBalance, createdDate, 
                       modifiedDate, isActive
                FROM Customer 
                WHERE customerId = @customerId AND isActive = 1";

            var customer = await connection.QueryFirstOrDefaultAsync<CustomerDto>(sql, new { customerId });
            
            if (customer != null && string.IsNullOrWhiteSpace(customer.Country))
                customer.Country = "Cameroon";
            
            return customer;
        }

        public async Task<CustomerSearchResponseDto> SearchCustomersAsync(CustomerSearchDto searchRequest)
        {
            using var connection = new SqliteConnection(_connectionString);

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

            var searchParam = $"%{searchRequest.SearchTerm}%";
            var parameters = new { search = searchParam, pageSize = searchRequest.PageSize, offset = (searchRequest.Page - 1) * searchRequest.PageSize };

            // Get total count
            var countSql = $"SELECT COUNT(*) FROM Customer {whereClause} {searchClause}";
            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, string.IsNullOrWhiteSpace(searchRequest.SearchTerm) ? null : new { search = searchParam });

            // Get paginated results
            var dataSql = $@"
                SELECT customerId, firstName, lastName, gender, contactPerson, email, 
                       phone, date_of_birth, creditLimit, address, city, state, postalCode, 
                       country, taxId, type, openingBalance, createdDate, 
                       modifiedDate, isActive
                FROM Customer 
                {whereClause} {searchClause}
                ORDER BY firstName, lastName
                LIMIT @pageSize OFFSET @offset";

            var customers = (await connection.QueryAsync<CustomerDto>(dataSql, parameters)).ToList();
            
            // Post-process to handle defaults
            foreach (var customer in customers)
            {
                if (string.IsNullOrWhiteSpace(customer.Country))
                    customer.Country = "Cameroon";
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

            var sql = @"
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

            var stats = await connection.QueryFirstOrDefaultAsync<CustomerStatisticsDto>(sql) ?? new CustomerStatisticsDto();

            // Get most common country
            var countrySql = @"
                SELECT country
                FROM Customer 
                WHERE country IS NOT NULL AND country != '' AND isActive = 1
                GROUP BY country 
                ORDER BY COUNT(*) DESC 
                LIMIT 1";
            stats.MostCommonCountry = await connection.QueryFirstOrDefaultAsync<string>(countrySql) ?? "";

            // Get most common city
            var citySql = @"
                SELECT city
                FROM Customer 
                WHERE city IS NOT NULL AND city != '' AND isActive = 1
                GROUP BY city 
                ORDER BY COUNT(*) DESC 
                LIMIT 1";
            stats.MostCommonCity = await connection.QueryFirstOrDefaultAsync<string>(citySql) ?? "";

            stats.TotalCurrentBalance = stats.TotalOpeningBalance;

            return stats;
        }
    }
}