using Microsoft.Extensions.DependencyInjection;

namespace OrderManagement.Application;

/// <summary>
/// Composition root for the Application layer (use cases and behaviours).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Use-case handlers, validators and pipeline behaviours are registered here
        // as the application logic is implemented.
        return services;
    }
}
