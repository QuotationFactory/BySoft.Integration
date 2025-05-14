using System;
using System.IO;
using System.Linq;
using MediatR;
using MetalHeaven.Integration.Shared.Classes;
using MetalHeaven.Integration.Shared.Extensions;
using MetalHeaven.Integration.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QF.BySoft.Entities;

namespace QF.BySoft.Integration.Features.AgentOutputFile;

/// <summary>
///     Service that watches on the output directory of the agent for *.json files
///     Publishes an AgentOutputFileCreated notification if a file is created
/// </summary>
public class AgentOutputFileWatcherService : FileWatcherService
{
    private readonly ILogger<AgentOutputFileWatcherService> _logger;
    private readonly IMediator _mediator;

    public AgentOutputFileWatcherService(IMediator mediator, IOptions<AgentSettings> options, ILogger<AgentOutputFileWatcherService> logger)
        : base(options)
    {
        _mediator = mediator;
        _logger = logger;

        // add file watcher to the agent output directory
        var directory = AgentSettings.GetOrCreateAgentOutputDirectory(Constants.AgentIntegrationName, true);
        ProcessExistingFiles(directory, ".json");
        AddFileWatcher(directory, "*.json");
        _logger.LogInformation("File watch added on: '{Directory}' with filter: *.json", directory);
    }

#pragma warning disable VSTHRD100
    private async void ProcessExistingFiles(string directory, string extensionfilter)
#pragma warning restore VSTHRD100
    {
        var existingFiles = Directory.EnumerateFiles(directory).Where(x => Path.GetExtension(x) == extensionfilter).ToArray();
        if (!existingFiles.Any())
        {
            return;
        }

        foreach (var file in existingFiles)
        {
            await _mediator.Publish(new AgentOutputFileCreated(file));
        }
    }

#pragma warning disable VSTHRD100
    protected override async void OnAllChanges(object sender, FileSystemEventArgs e)
#pragma warning restore VSTHRD100
    {
        try
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    await _mediator.Publish(new AgentOutputFileCreated(e.FullPath));
                    break;
                case WatcherChangeTypes.Deleted:
                    break;
                case WatcherChangeTypes.Changed:
                    break;
                case WatcherChangeTypes.Renamed:
                    break;
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
    }
}
