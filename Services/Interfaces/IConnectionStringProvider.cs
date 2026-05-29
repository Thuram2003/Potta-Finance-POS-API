namespace PottaAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for providing database connection string
    /// </summary>
    public interface IConnectionStringProvider
    {
        /// <summary>
        /// Get the database connection string
        /// </summary>
        string GetConnectionString();
    }
}