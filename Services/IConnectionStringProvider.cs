namespace PottaAPI.Services
{
    /// <summary>
    /// Interface for providing database connection string
    /// </summary>
    public interface IConnectionStringProvider
    {
        /// <summary>
        /// Get the database connection string
        /// </summary>
        /// <returns>Connection string</returns>
        string GetConnectionString();
    }
}