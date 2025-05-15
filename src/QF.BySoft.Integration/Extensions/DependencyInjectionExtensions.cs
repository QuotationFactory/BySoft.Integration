using Microsoft.Extensions.DependencyInjection;
using QF.BySoft.Integration.Features.AgentOutputFile;

namespace QF.BySoft.Integration.Extensions;

public static class DependencyInjectionExtensions
{
    public static void AddFileWatchFeature(this IServiceCollection services)
    {

        // register agent output file watcher service
        services.AddHostedService<AgentOutputFileWatcherService>();

        // register MediatR with current assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AgentOutputFileWatcherService).Assembly));
    }
}
