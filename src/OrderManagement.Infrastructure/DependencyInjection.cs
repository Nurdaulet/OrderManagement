using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Abstractions;
using OrderManagement.Infrastructure.ExternalApi;
using OrderManagement.Infrastructure.GoogleSheets;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer (persistence and external integrations).
/// </summary>
public static class DependencyInjection
{
    private const string DefaultConnectionString = "Data Source=ordermanagement.db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection") ?? DefaultConnectionString;

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        // Expose the context to the Application layer through its abstraction (same scoped instance).
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Mock external document source (reads a local JSON file).
        services.Configure<ExternalDocumentSourceOptions>(
            configuration.GetSection(ExternalDocumentSourceOptions.SectionName));
        services.AddScoped<IExternalDocumentProvider, JsonExternalDocumentProvider>();

        // Mock Google Sheets logger (appends to a local CSV). Singleton so writes are serialised.
        services.Configure<CsvGoogleSheetLoggerOptions>(
            configuration.GetSection(CsvGoogleSheetLoggerOptions.SectionName));
        services.AddSingleton<IGoogleSheetLogger, CsvGoogleSheetLogger>();

        // Applies migrations and seeds sample orders on startup.
        services.AddScoped<AppDbInitializer>();

        return services;
    }
}
