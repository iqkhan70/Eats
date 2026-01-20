using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace TraditionEats.BuildingBlocks.Configuration;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Adds shared configuration from the BuildingBlocks project.
    /// This allows all services to use the same base configuration while overriding service-specific settings.
    /// Call this BEFORE adding service-specific appsettings files so they can override shared values.
    /// </summary>
    public static IConfigurationBuilder AddSharedConfiguration(this IConfigurationBuilder builder, IHostEnvironment environment)
    {
        // Try multiple paths to find the shared config
        var possiblePaths = new[]
        {
            // From service directory (when running from service folder)
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "shared", "TraditionEats.BuildingBlocks", "Configuration", "appsettings.Shared.json"),
            // From solution root
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "shared", "TraditionEats.BuildingBlocks", "Configuration", "appsettings.Shared.json"),
            // Absolute path from base directory
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "shared", "TraditionEats.BuildingBlocks", "Configuration", "appsettings.Shared.json"),
            // From bin/Debug/net8.0 when running
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "shared", "TraditionEats.BuildingBlocks", "Configuration", "appsettings.Shared.json")
        };

        string? sharedConfigPath = null;
        foreach (var path in possiblePaths)
        {
            try
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    sharedConfigPath = normalizedPath;
                    break;
                }
            }
            catch
            {
                // Skip invalid paths
                continue;
            }
        }

        if (sharedConfigPath != null)
        {
            // Insert at the beginning so service-specific configs can override
            // Make it optional to avoid errors during design-time (EF migrations)
            builder.Sources.Insert(0, new JsonConfigurationSource
            {
                Path = sharedConfigPath,
                Optional = true, // Always optional to avoid design-time issues
                ReloadOnChange = true
            });
        }

        // Add environment-specific shared config if it exists
        var sharedEnvConfigPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "shared", "TraditionEats.BuildingBlocks", "Configuration", $"appsettings.Shared.{environment.EnvironmentName}.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "shared", "TraditionEats.BuildingBlocks", "Configuration", $"appsettings.Shared.{environment.EnvironmentName}.json")
        };

        foreach (var path in sharedEnvConfigPaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (File.Exists(normalizedPath))
            {
                builder.Sources.Insert(1, new JsonConfigurationSource
                {
                    Path = normalizedPath,
                    Optional = true,
                    ReloadOnChange = true
                });
                break;
            }
        }

        return builder;
    }
}
