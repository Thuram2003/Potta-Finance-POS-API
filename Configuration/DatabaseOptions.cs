namespace PottaAPI.Configuration
{
    /// <summary>
    /// Configuration options for database connection
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>
        /// Name of the database file
        /// </summary>
        public string FileName { get; set; } = "pottadb.db";

        /// <summary>
        /// List of paths to search for the database file
        /// </summary>
        public List<string> SearchPaths { get; set; } = new();

        /// <summary>
        /// Enable detailed error messages (development only)
        /// </summary>
        public bool EnableDetailedErrors { get; set; }

        /// <summary>
        /// Command timeout in seconds
        /// </summary>
        public int CommandTimeout { get; set; } = 30;
    }
}
