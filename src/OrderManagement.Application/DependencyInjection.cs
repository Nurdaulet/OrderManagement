using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderManagement.Application.Features.Synchronization;

namespace OrderManagement.Application;

/// <summary>
/// Composition root for the Application layer (use cases and behaviours).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDocumentSyncService, DocumentSyncService>();

        return services;
    }
}
