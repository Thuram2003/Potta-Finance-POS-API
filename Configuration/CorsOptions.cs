namespace PottaAPI.Configuration
{
    /// <summary>
    /// Configuration options for CORS policy
    /// </summary>
    public class CorsOptions
    {
        public const string SectionName = "Cors";

        /// <summary>
        /// Name of the CORS policy
        /// </summary>
        public string PolicyName { get; set; } = "AllowAllDevices";

        /// <summary>
        /// List of allowed origins (* for all)
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new() { "*" };

        /// <summary>
        /// List of allowed HTTP methods
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

        /// <summary>
        /// List of allowed headers (* for all)
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new() { "*" };

        /// <summary>
        /// Allow credentials in CORS requests
        /// </summary>
        public bool AllowCredentials { get; set; }
    }
}
