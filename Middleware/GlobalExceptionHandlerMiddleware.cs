using PottaAPI.Models;
using PottaAPI.Models.Common;
using System.Net;

namespace PottaAPI.Middleware
{
    // Global exception handler middleware
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Unhandled exception occurred. Path: {Path}, Method: {Method}, User: {User}", 
                    context.Request.Path, 
                    context.Request.Method,
                    context.User?.Identity?.Name ?? "Anonymous");
                
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var (statusCode, error, details) = exception switch
            {
                ArgumentNullException argNullEx => (
                    StatusCodes.Status400BadRequest,
                    "Missing required parameter",
                    argNullEx.Message
                ),
                ArgumentException argEx => (
                    StatusCodes.Status400BadRequest,
                    "Invalid argument",
                    argEx.Message
                ),
                KeyNotFoundException => (
                    StatusCodes.Status404NotFound,
                    "Resource not found",
                    exception.Message
                ),
                FileNotFoundException fileEx => (
                    StatusCodes.Status404NotFound,
                    "File not found",
                    fileEx.Message
                ),
                UnauthorizedAccessException => (
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized access",
                    "You do not have permission to access this resource"
                ),
                InvalidOperationException => (
                    StatusCodes.Status400BadRequest,
                    "Invalid operation",
                    exception.Message
                ),
                TimeoutException => (
                    StatusCodes.Status408RequestTimeout,
                    "Request timeout",
                    "The request took too long to process"
                ),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Internal server error",
                    _environment.IsDevelopment() 
                        ? exception.Message 
                        : "An unexpected error occurred. Please try again later."
                )
            };

            context.Response.StatusCode = statusCode;

            var response = new ErrorResponseDto
            {
                Error = error,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            // Add stack trace and inner exception in development
            if (_environment.IsDevelopment() && statusCode == StatusCodes.Status500InternalServerError)
            {
                var debugInfo = new System.Text.StringBuilder();
                debugInfo.AppendLine($"\n\nException Type: {exception.GetType().FullName}");
                debugInfo.AppendLine($"Stack Trace:\n{exception.StackTrace}");
                
                if (exception.InnerException != null)
                {
                    debugInfo.AppendLine($"\n\nInner Exception: {exception.InnerException.Message}");
                    debugInfo.AppendLine($"Inner Stack Trace:\n{exception.InnerException.StackTrace}");
                }

                response.Details += debugInfo.ToString();
            }

            await context.Response.WriteAsJsonAsync(response);
        }
    }

    /// <summary>
    /// Extension method for registering the global exception handler middleware
    /// </summary>
    public static class GlobalExceptionHandlerMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        }
    }
}
