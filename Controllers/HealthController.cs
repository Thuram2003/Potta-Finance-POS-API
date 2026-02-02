using Microsoft.AspNetCore.Mvc;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public HealthController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Basic health check endpoint to verify if the API is running
        /// Used by desktop app to check if API server is online
        /// </summary>
        /// <returns>OK status if the API is healthy</returns>
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                message = "PottaAPI is running successfully"
            });
        }
    }
}
