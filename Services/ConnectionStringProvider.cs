namespace PottaAPI.Services
{
    /// <summary>
    /// Provides database connection string using the same logic as DatabaseService
    /// </summary>
    public class ConnectionStringProvider : IConnectionStringProvider
    {
        private readonly string _connectionString;

        public ConnectionStringProvider()
        {
            string dbPath = FindDatabasePath();
            
            if (dbPath == null)
            {
                throw new FileNotFoundException(
                    "Database file 'pottadb.db' not found. Please ensure the POS application is installed correctly. " +
                    "The database should be located in the same directory as the application executables.");
            }

            Console.WriteLine($"Database found at: {dbPath}");
            _connectionString = $"Data Source={dbPath};Foreign Keys=True;Mode=ReadWrite";
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }

        private string FindDatabasePath()
        {
            // Get the base directory where the API is running from
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            
            // Check if we're in Debug mode (development environment)
            var isDebugMode = baseDirectory.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase);
            
            List<string> possiblePaths = new List<string>();

            if (isDebugMode)
            {
                // Navigate from API bin folder to WPF app bin folder
                var debugPath = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\..\Potta Finance\bin\Debug\net8.0-windows\pottadb.db"));
                possiblePaths.Add(debugPath);
                Console.WriteLine($"Running in DEBUG mode. Looking for database at: {debugPath}");
            }
            else
            {
                // Production mode: Database is in the same directory as the executables
                // When installed via Inno Setup, both PottaAPI.exe and Potta Finance.exe are in the same folder
                possiblePaths.Add(Path.Combine(baseDirectory, "pottadb.db"));
                
                // Also check parent directory (in case API is in a subfolder)
                var parentDir = Directory.GetParent(baseDirectory)?.FullName;
                if (parentDir != null)
                {
                    possiblePaths.Add(Path.Combine(parentDir, "pottadb.db"));
                }
                
                // Check common installation paths
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                possiblePaths.Add(Path.Combine(localAppData, "Programs", "Potta Finance POS", "pottadb.db"));
                possiblePaths.Add(Path.Combine(localAppData, "Potta Finance POS", "pottadb.db"));
                
                Console.WriteLine($"Running in PRODUCTION mode. Searching for database...");
            }

            // Search for the database file
            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"Checking: {path}");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Log all attempted paths for debugging
            Console.WriteLine("Database not found in any of the following locations:");
            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"  - {path}");
            }

            return null;
        }
    }
}