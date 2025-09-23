using Microsoft.AspNetCore.Cors;
using Microsoft.Data.Sqlite;
using PottaAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add static files support for serving test page
builder.Services.AddDirectoryBrowser();

// Add CORS for mobile app access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobileApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register database service
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable static files and directory browsing
app.UseStaticFiles();
app.UseDirectoryBrowser();

app.UseCors("AllowMobileApp");
app.UseAuthorization();
app.MapControllers();

// Test database connection on startup
using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
    try
    {
        await dbService.TestConnectionAsync();
        Console.WriteLine("Database connection successful");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database connection failed: {ex.Message}");
    }
}

Console.WriteLine("PottaAPI is running on http://localhost:5001");
Console.WriteLine("📱 Mobile apps can connect to sync data");

app.Run("http://localhost:5001");
