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

        /// <summary>
        /// Base path to the desktop application's Images folder (relative to API executable)
        /// NOTE: This is now optional and overridden by conditional compilation in Program.cs
        /// The API automatically determines the correct path based on DEBUG/RELEASE mode
        /// </summary>
        public string ImageBasePath { get; set; } = "";

        /// <summary>
        /// Optional external/tunnel URL to use in QR codes instead of local IP.
        /// Set this when using a dev tunnel or reverse proxy (e.g. https://xxxx-5001.use.devtunnels.ms)
        /// Leave empty to use local IP auto-detection.
        /// </summary>
        public string? ExternalUrl { get; set; }
    }
}
