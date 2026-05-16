using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
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

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting PottaAPI application");
    Log.Information("Current Directory: {Directory}", Directory.GetCurrentDirectory());
    Log.Information(".NET Version: {Version}", Environment.Version);

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithThreadId());

    var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    var apiOptions = builder.Configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

    builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
    builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
    builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
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

        // Load XML comments so Swagger shows /// summaries, remarks, and response docs
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);

        // Group endpoints by controller tag
        c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
        c.DocInclusionPredicate((_, _) => true);
    });

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();

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

    builder.Services.AddResponseCaching();

    builder.Services.AddMemoryCache();

    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    builder.Services.AddHealthChecks();

    builder.Services.AddDirectoryBrowser();

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

    builder.Services.AddSingleton<IConnectionStringProvider, ConnectionStringProvider>();

    builder.Services.AddSingleton<IDatabaseService>(provider =>
    {
        var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
        return new DatabaseService(connectionStringProvider);
    });

    builder.Services.AddSingleton<ICustomerService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new CustomerService(connectionStringProvider.GetConnectionString());
});

builder.Services.AddSingleton<IItemService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    var cache = provider.GetRequiredService<IMemoryCache>();
    return new ItemService(connectionStringProvider.GetConnectionString(), cache);
});

builder.Services.AddSingleton<IOrderService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    var taxService = provider.GetRequiredService<ITaxService>();
    return new OrderService(connectionStringProvider.GetConnectionString(), taxService);
});

builder.Services.AddSingleton<ITableService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new TableService(connectionStringProvider.GetConnectionString());
});

builder.Services.AddSingleton<IStaffService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new StaffService(connectionStringProvider.GetConnectionString());
});

builder.Services.AddSingleton<ITaxService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    var logger = provider.GetRequiredService<ILogger<TaxService>>();
    return new TaxService(connectionStringProvider, logger);
});

builder.Services.AddSingleton<IDiscountService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    var logger = provider.GetRequiredService<ILogger<DiscountService>>();
    return new DiscountService(connectionStringProvider, logger);
});

builder.Services.AddSingleton<IFloorPlanService>(provider =>
{
    var connectionStringProvider = provider.GetRequiredService<IConnectionStringProvider>();
    return new FloorPlanService(connectionStringProvider.GetConnectionString());
});

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
    Log.Information("========================================");
    Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("Database file: {DatabaseFile}", databaseOptions.FileName);
    Log.Information("Database search paths: {SearchPaths}", string.Join(", ", databaseOptions.SearchPaths));
    Log.Information("API Port: {Port}", apiOptions.Port);
    Log.Information("CORS Policy: {Policy}", corsOptions.PolicyName);
    Log.Information("Allowed Origins: {Origins}", string.Join(", ", corsOptions.AllowedOrigins));
    Log.Information("========================================");

    string serverIpAddress = GetServerIPAddress();
    Log.Information("Server IP Address: {IpAddress}", serverIpAddress);

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
    
    app.UseGlobalExceptionHandler();

    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/images") && 
                   !context.Request.Path.StartsWithSegments("/swagger") &&
                   !context.Request.Path.Value.EndsWith(".html") &&
                   !context.Request.Path.Value.EndsWith(".css") &&
                   !context.Request.Path.Value.EndsWith(".js"),
        appBuilder => appBuilder.UseIpRateLimiting()
    );

    app.UseResponseCaching();

    app.UseSwagger();
    app.UseSwaggerUI(Theme.Dark);

    app.Use(async (context, next) =>
    {
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/swagger");
        return;
    }
    await next();
    });

    app.UseStaticFiles();

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
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
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

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health/live");

    app.MapControllers();

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