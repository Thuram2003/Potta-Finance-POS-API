namespace PottaAPI.Models.Common
{
    // Generic API response wrapper
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
