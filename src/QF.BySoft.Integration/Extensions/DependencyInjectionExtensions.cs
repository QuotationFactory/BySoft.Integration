using Microsoft.Extensions.DependencyInjection;
using QF.BySoft.Integration.Features.AgentOutputFile;

namespace QF.BySoft.Integration.Extensions;

public static class DependencyInjectionExtensions
{
    public static void AddFileWatchFeature(this IServiceCollection services)
    {
        // register agent output file created handler
        services.AddTransient<AgentOutputFileCreatedHandler>();

        // register agent output file watcher service
        services.AddHostedService<AgentOutputFileWatcherService>();
    }
}
