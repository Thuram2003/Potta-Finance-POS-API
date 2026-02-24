using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using PottaAPI.Configuration;
using PottaAPI.Middleware;
using PottaAPI.Services;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using AspNetCore.Swagger.Themes;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using AspNetCoreRateLimit;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting PottaAPI application");
    Log.Information("Current Directory: {Directory}", Directory.GetCurrentDirectory());
    Log.Information(".NET Version: {Version}", Environment.Version);

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithThreadId());

    // Bind configuration options
    var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    var apiOptions = builder.Configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

    // Register configuration options
    builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
    builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
    builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
    // Configure Swagger (clean, no XML comments)
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = apiOptions.Title,
            Version = apiOptions.Version,
            Description = apiOptions.Description,
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "Potta Finance Support",
                Email = "support@pottafinance.com"
            }
        });

        // XML comments disabled - keeps Swagger clean and simple
    });

    // Add FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();

    // Configure automatic validation error responses
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => new
                {
                    Field = e.Key,
                    Message = x.ErrorMessage
                }))
                .ToList();

            var response = new
            {
                Error = "Validation failed",
                Details = "One or more validation errors occurred",
                ValidationErrors = errors,
                Timestamp = DateTime.UtcNow
            };

            return new BadRequestObjectResult(response);
        };
    });

    // Add Response Caching
    builder.Services.AddResponseCaching();

    // Add Memory Cache for rate limiting
    builder.Services.AddMemoryCache();

    // Add Rate Limiting
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // Add Health Checks (simplified - just check if API is running)
    builder.Services.AddHealthChecks();


    // Add static files support for serving test page and images
    builder.Services.AddDirectoryBrowser();

    // Configure CORS from appsettings
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(corsOptions.PolicyName, policy =>
        {
            if (corsOptions.AllowedOrigins.Contains("*"))
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
                      .SetIsOriginAllowedToAllowWildcardSubdomains();
            }

            if (corsOptions.AllowedMethods.Contains("*"))
            {
                policy.AllowAnyMethod();
            }
            else
            {
                policy.WithMethods(corsOptions.AllowedMethods.ToArray());
            }

            if (corsOptions.AllowedHeaders.Contains("*"))
            {
                policy.AllowAnyHeader();
            }
            else
            {
                policy.WithHeaders(corsOptions.AllowedHeaders.ToArray());
            }

            if (corsOptions.AllowCredentials)
            {
                policy.AllowCredentials();
            }
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
    var taxService = provider.GetRequiredService<ITaxService>();
    return new OrderService(connectionStringProvider.GetConnectionString(), taxService);
});

// Register table services
builder.Services.AddSingleton<ITableService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new TableService(connectionStringProvider.GetConnectionString());
});

// Register staff services
builder.Services.AddSingleton<IStaffService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new StaffService(connectionStringProvider.GetConnectionString());
});

// Register tax services
builder.Services.AddSingleton<ITaxService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    var logger = provider.GetRequiredService<ILogger<TaxService>>();
    return new TaxService(connectionStringProvider, logger);
});

// Register discount services
builder.Services.AddSingleton<IDiscountService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    var logger = provider.GetRequiredService<ILogger<DiscountService>>();
    return new DiscountService(connectionStringProvider, logger);
});

// Register floor plan services
builder.Services.AddSingleton<IFloorPlanService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new FloorPlanService(connectionStringProvider.GetConnectionString());
});

// Register restaurant operations services
builder.Services.AddSingleton<IRestaurantOperationsService>(provider =>
{
    var databaseService = provider.GetRequiredService<IDatabaseService>() as DatabaseService 
        ?? throw new InvalidOperationException("DatabaseService is required");
    var orderService = provider.GetRequiredService<IOrderService>();
    var staffService = provider.GetRequiredService<IStaffService>();
    var tableService = provider.GetRequiredService<ITableService>();
    
    return new RestaurantOperationsService(databaseService, orderService, staffService, tableService);
});

    var app = builder.Build();

    // Log environment and configuration
    var environment = app.Environment.EnvironmentName;
    Log.Information("========================================");
    Log.Information("Environment: {Environment}", environment);
    Log.Information("Database file: {DatabaseFile}", databaseOptions.FileName);
    Log.Information("Database search paths: {SearchPaths}", string.Join(", ", databaseOptions.SearchPaths));
    Log.Information("API Port: {Port}", apiOptions.Port);
    Log.Information("CORS Policy: {Policy}", corsOptions.PolicyName);
    Log.Information("Allowed Origins: {Origins}", string.Join(", ", corsOptions.AllowedOrigins));
    Log.Information("========================================");

    // Get and display the server IP address
    string serverIpAddress = GetServerIPAddress();
    Log.Information("Server IP Address: {IpAddress}", serverIpAddress);

    // Configure the HTTP request pipeline.
    
    // Add Serilog request logging (real-time HTTP logs like NestJS)
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress);
        };
    });
    
    // Global exception handler (must be early in pipeline)
    app.UseGlobalExceptionHandler();

    // Add IP Rate Limiting (but exempt static files)
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/images") && 
                   !context.Request.Path.StartsWithSegments("/swagger") &&
                   !context.Request.Path.Value.EndsWith(".html") &&
                   !context.Request.Path.Value.EndsWith(".css") &&
                   !context.Request.Path.Value.EndsWith(".js"),
        appBuilder => appBuilder.UseIpRateLimiting()
    );

    // Add Response Caching
    app.UseResponseCaching();

    // Enable Swagger in all environments (development and production)
    app.UseSwagger();
    app.UseSwaggerUI(Theme.Dark);

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

    // Enable static files from wwwroot (default)
    app.UseStaticFiles();

    // Serve images from desktop application's Images folder
    var desktopAppImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, apiOptions.ImageBasePath);
    var resolvedImagePath = Path.GetFullPath(desktopAppImagePath);
    
    if (Directory.Exists(resolvedImagePath))
    {
        Log.Information("✓ Serving images from: {ImagePath}", resolvedImagePath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(resolvedImagePath),
            RequestPath = "/images",
            ServeUnknownFileTypes = true,
            DefaultContentType = "image/png",
            OnPrepareResponse = ctx =>
            {
                // Add CORS headers for images
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                // Cache images for 1 hour
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600");
            }
        });
    }
    else
    {
        Log.Warning("✗ Desktop app image folder not found at: {ImagePath}", resolvedImagePath);
        Log.Warning("  Configured path: {ConfigPath}", apiOptions.ImageBasePath);
        Log.Warning("  Image serving may not work correctly");
    }

    app.UseDirectoryBrowser();

    app.UseCors(corsOptions.PolicyName);
    app.UseAuthorization();

    // Map Health Check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health/live");

    app.MapControllers();

    // Test database connection on startup
    using (var scope = app.Services.CreateScope())
    {
        var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
        try
        {
            Log.Information("Testing database connection...");
            await dbService.TestConnectionAsync();
            Log.Information("✓ Database connection successful");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "✗ Database connection failed - API will continue but database operations may fail");
            Log.Error("Database search paths: {SearchPaths}", string.Join(", ", databaseOptions.SearchPaths));
        }
    }

    var serverUrl = $"http://0.0.0.0:{apiOptions.Port}";
    Log.Information("========================================");
    Log.Information("PottaAPI is running on {ServerUrl}", serverUrl);
    Log.Information("Local access: http://localhost:{Port}", apiOptions.Port);
    Log.Information("Network access: http://{IpAddress}:{Port}", serverIpAddress, apiOptions.Port);
    Log.Information("Swagger UI: http://localhost:{Port}/swagger", apiOptions.Port);
    Log.Information("Health Check: http://localhost:{Port}/health", apiOptions.Port);
    Log.Information("========================================");

    app.Run(serverUrl);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

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