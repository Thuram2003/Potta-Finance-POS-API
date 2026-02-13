namespace PottaAPI.Models.Common
{
    // Standard error response format
    public class ErrorResponseDto
    {
        public string Error { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
