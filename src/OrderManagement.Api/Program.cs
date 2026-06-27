using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using OrderManagement.Api.Middleware;
using OrderManagement.Application;
using OrderManagement.Infrastructure;
using OrderManagement.Infrastructure.Persistence;
using Serilog;

// Two-stage Serilog initialisation: a bootstrap logger captures failures that occur
// before the host (and its configured logger) is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Order Management API");

    var builder = WebApplication.CreateBuilder(args);

    // Replace the default logging with Serilog, reading its configuration from appsettings.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    // Global exception handling -> RFC 7807 ProblemDetails.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Serialise enums as their string names (e.g. "Invoice", "Success").
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Swagger / OpenAPI.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Order Management API",
            Version = "v1",
            Description = "Synchronises financial documents between Order Management and an external system."
        });

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // Application and Infrastructure layers.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // Apply migrations and seed sample orders so the API is runnable out of the box.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<AppDbInitializer>();
        await initializer.InitializeAsync();
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.MapControllers();

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Order Management API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Entry point type, exposed so integration tests can use <c>WebApplicationFactory</c>.</summary>
public partial class Program;
