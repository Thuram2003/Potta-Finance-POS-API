namespace PottaAPI.Configuration
{
    /// <summary>
    /// Configuration options for API settings
    /// </summary>
    public class ApiOptions
    {
        public const string SectionName = "Api";

        /// <summary>
        /// Port number for the API server
        /// </summary>
        public int Port { get; set; } = 5001;

        /// <summary>
        /// API version
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// API title for documentation
        /// </summary>
        public string Title { get; set; } = "Potta Finance POS API";

        /// <summary>
        /// API description for documentation
        /// </summary>
        public string Description { get; set; } = "REST API for Potta Finance POS System";
    }
}
