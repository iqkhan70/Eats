using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TraditionalEats.BuildingBlocks.Observability;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Skips metrics on macOS to avoid PAL_SEHException crash during OpenTelemetry metrics initialization.
    /// See: https://github.com/dotnet/runtime/issues/78271 and related runtime issues.
    /// </summary>
    private static bool SkipMetrics => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static IServiceCollection AddOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
    {
        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        if (!SkipMetrics)
        {
            builder.WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());
        }

        builder.WithLogging(logging => logging
            .AddConsoleExporter());

        return services;
    }
}
