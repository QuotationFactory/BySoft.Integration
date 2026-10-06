using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QF.BySoft.Entities;
using QF.Integration.Common.FileWatcher;

namespace QF.BySoft.Integration.Features.AgentOutputFile;

/// <summary>
///     Service that watches on the output directory of the agent for *.json files
///     Calls AgentOutputFileCreatedHandler if a file is created
/// </summary>
public class AgentOutputFileWatcherService : FileWatcherService
{
    private readonly ILogger<AgentOutputFileWatcherService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AgentOutputFileWatcherService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BySoftIntegrationSettings> options,
        ILogger<AgentOutputFileWatcherService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        // add file watcher to the agent output directory
        var bySoftIntegrationSettings = options.Value;
        var directory = bySoftIntegrationSettings.GetOrCreateAgentOutputDirectory(Constants.IntegrationName, true);
        if (bySoftIntegrationSettings.NumberOfConcurrentTasks > 1)
        {
            _semaphore = new(bySoftIntegrationSettings.NumberOfConcurrentTasks, bySoftIntegrationSettings.NumberOfConcurrentTasks);
            _logger.LogInformation("Semaphore initialized with {NumberOfConcurrentTasks} concurrent tasks", bySoftIntegrationSettings.NumberOfConcurrentTasks);
        }
        AddFileWatcher(directory, "*.json");
        _logger.LogInformation("File watch added on: '{Directory}' with filter: *.json", directory);
    }

#pragma warning disable VSTHRD100
    protected override async void OnAllChanges(object sender, FileSystemEventArgs e)
#pragma warning restore VSTHRD100
    {
        try
        {
            await _semaphore.WaitAsync();
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    await HandleCreatedFileAsync(e.FullPath, CancellationToken.None);
                    break;
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Renamed:
                case WatcherChangeTypes.All:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing {Event} for file {FilePath}", e.ChangeType, e.FullPath);
        }
        finally
        {
            _semaphore.Release();
        }
    }

#pragma warning disable VSTHRD100
    protected override async void OnExistingFile(object sender, FileSystemEventArgs e)
#pragma warning restore VSTHRD100
    {
        try
        {
            await _semaphore.WaitAsync();
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Renamed:
                    break;
                case WatcherChangeTypes.All:
                    await HandleCreatedFileAsync(e.FullPath, CancellationToken.None);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing {Event} for file {FilePath}", e.ChangeType, e.FullPath);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task HandleCreatedFileAsync(string filePath, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AgentOutputFileCreatedHandler>();
        await handler.HandleAsync(new AgentOutputFileCreated(filePath), cancellationToken);
    }
}
