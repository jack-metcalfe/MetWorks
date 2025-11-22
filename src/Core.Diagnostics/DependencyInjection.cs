using Microsoft.Extensions.DependencyInjection;
using System;

namespace MetWorks.Core.Diagnostics;

public static class DependencyInjection
{
    /// <summary>
    /// Registers DiagnosticsRegistry as a singleton. The registry is loaded once at registration time.
    /// </summary>
    public static IServiceCollection AddProjectDiagnostics(this IServiceCollection services, string? diagnosticsPath = null)
    {
        services.AddSingleton(provider =>
        {
            return DiagnosticsRegistry.LoadFromFile(diagnosticsPath);
        });

        return services;
    }
}
