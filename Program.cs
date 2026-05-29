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
using PottaAPI.Services.Interfaces;

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

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);

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
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
                      .SetIsOriginAllowedToAllowWildcardSubdomains();

            if (corsOptions.AllowedMethods.Contains("*"))
                policy.AllowAnyMethod();
            else
                policy.WithMethods(corsOptions.AllowedMethods.ToArray());

            if (corsOptions.AllowedHeaders.Contains("*"))
                policy.AllowAnyHeader();
            else
                policy.WithHeaders(corsOptions.AllowedHeaders.ToArray());

            if (corsOptions.AllowCredentials)
                policy.AllowCredentials();
        });
    });

    // ── Core Services ──
    builder.Services.AddSingleton<IConnectionStringProvider, ConnectionStringProvider>();
    builder.Services.AddScoped<IDatabaseService, DatabaseService>();

    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddSingleton<IItemService, ItemService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<ITableService, TableService>();
    builder.Services.AddScoped<IStaffService, StaffService>();
    builder.Services.AddScoped<ITaxService, TaxService>();
    builder.Services.AddScoped<IDiscountService, DiscountService>();
    builder.Services.AddScoped<IFloorPlanService, FloorPlanService>();
    builder.Services.AddScoped<IOrderOperationsService, OrderOperationsService>();
    builder.Services.AddScoped<IBillRequestService, BillRequestService>();
    builder.Services.AddScoped<IOrderTaxService, OrderTaxService>();

    var app = builder.Build();

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
                   !context.Request.Path.Value?.EndsWith(".html") != true &&
                   !context.Request.Path.Value?.EndsWith(".css") != true &&
                   !context.Request.Path.Value?.EndsWith(".js") != true,
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

    string resolvedImagePath;
#if DEBUG
    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    resolvedImagePath = Path.GetFullPath(Path.Combine(baseDirectory, "../../../../Potta Finance/bin/Debug/net8.0-windows/Images"));
    Log.Information("DEBUG MODE: Image path determination");
#else
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    resolvedImagePath = Path.Combine(localAppData, "Instanvi", "Potta Finance POS", "Images");
    Log.Information("PRODUCTION MODE: Image path determination");
#endif

    if (Directory.Exists(resolvedImagePath))
    {
        Log.Information("Serving images from: {ImagePath}", resolvedImagePath);
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
        Log.Warning("Desktop app image folder not found at: {ImagePath}", resolvedImagePath);
        try
        {
            Directory.CreateDirectory(resolvedImagePath);
            Log.Information("Created image directory: {ImagePath}", resolvedImagePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create image directory");
        }
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
            Log.Information("Database connection successful");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database connection failed - API will continue but database operations may fail");
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
            return address.ToString();
    }
    return string.Empty;
}