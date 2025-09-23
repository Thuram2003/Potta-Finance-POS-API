using Microsoft.AspNetCore.Mvc;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Health check endpoint to verify if the API is running
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
