using System;
using System.Reflection;
using MetalHeaven.Agent.Shared.External.Classes;
using MetalHeaven.Agent.Shared.External.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using QF.BySoft.Entities;
using QF.BySoft.Entities.Repositories;
using QF.BySoft.Integration.Extensions;
using QF.BySoft.Integration.Features.BySoftIntegration;
using QF.BySoft.LocalData;
using QF.BySoft.LocalData.Helpers;
using QF.BySoft.Manufacturability;
using QF.BySoft.Manufacturability.Interfaces;
using Serilog;

namespace QF.BySoft.Integration;

public static class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"{GetBasePath()}\\logs\\log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            Log.Information("Starting host. Version: {Version}", version);

            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .UseWindowsService()
            .UseContentRoot(GetBasePath())
            .ConfigureServices((hostContext, services) =>
            {
                // register agent message serialization helper
                services.AddTransient<IAgentMessageSerializationHelper, ExternalAgentMessageSerializationHelper>();

                services.AddTransient<IMachineMappingRepository, MachineMappingRepository>();
                services.AddTransient<IMaterialMappingRepository, MaterialMappingRepository>();
                services.AddTransient<IBySoftManufacturabilityCheckBending, BySoftManufacturabilityCheckBending>();
                services.AddHttpClient<IBySoftIntegration, BySoftIntegration>();
                services.AddHttpClient<IBySoftApi, BySoftApi>();

                // register integration settings
                services.AddOptions<BySoftIntegrationSettings>().Bind(hostContext.Configuration.GetSection("BySoftIntegrationSettings"))
                    .ValidateDataAnnotations();

                // Log settings, so that in case of debugging at the client, we know what settings were used
                services.LogSettings();

                services.AddFileWatchFeature();
            });
    }

    private static void LogSettings(this IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var bySoftSettings = serviceProvider.GetService<IOptions<BySoftIntegrationSettings>>()?.Value;
        Log.Information("BySoft settings: {BySoftSettings}", JsonConvert.SerializeObject(bySoftSettings));
    }

    private static string GetBasePath()
    {
        return ApplicationInfo.GetApplicationBasePath();
    }
}
