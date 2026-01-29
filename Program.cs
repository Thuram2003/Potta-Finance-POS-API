using Microsoft.AspNetCore.Cors;
using Microsoft.Data.Sqlite;
using PottaAPI.Services;
using System.Net;
using System.Net.Sockets;
using AspNetCore.Swagger.Themes;  // Correct namespace for themes

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add static files support for serving test page
builder.Services.AddDirectoryBrowser();

// Add CORS for any device (mobile, web, other servers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDevices", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register connection string provider
builder.Services.AddSingleton<IConnectionStringProvider, ConnectionStringProvider>();

// Register database services
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

// Register customer services
builder.Services.AddSingleton<ICustomerService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new CustomerService(connectionStringProvider.GetConnectionString());
});

// Register item services
builder.Services.AddSingleton<IItemService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new ItemService(connectionStringProvider.GetConnectionString());
});

// Register order services
builder.Services.AddSingleton<IOrderService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new OrderService(connectionStringProvider.GetConnectionString());
});

// Register table services
builder.Services.AddSingleton<ITableService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new TableService(connectionStringProvider.GetConnectionString());
});

var app = builder.Build();

// Get and display the server IP address
string serverIpAddress = GetServerIPAddress();
Console.WriteLine($"Server IP Address: {serverIpAddress}");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(Theme.Dark);

    // Theme switcher (would allow switching to light/other modes)
    // app.UseSwaggerUI(Theme.Dark, options =>
    // {
    //     options.EnableThemeSwitcher();  // ← This would enable light/dark toggle
    // });
}

// Redirect root URL to Swagger UI (must be before static files/dir browser)
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/swagger");
        return;
    }
    await next();
});

// Enable static files and directory browsing
app.UseStaticFiles();
app.UseDirectoryBrowser();

app.UseCors("AllowAllDevices");
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

Console.WriteLine("PottaAPI is running on http://0.0.0.0:5001");
Console.WriteLine($"Local access: http://localhost:5001");
Console.WriteLine($"Network access: http://{serverIpAddress}:5001");

app.Run("http://0.0.0.0:5001");

string GetServerIPAddress()
{
    var hostName = Dns.GetHostName();
    var ipHostInfo = Dns.GetHostEntry(hostName);
    foreach (var address in ipHostInfo.AddressList)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return address.ToString();
        }
    }
    return string.Empty;
}