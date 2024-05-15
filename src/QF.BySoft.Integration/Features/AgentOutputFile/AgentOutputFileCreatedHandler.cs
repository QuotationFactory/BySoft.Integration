using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using QF.BySoft.Integration.Features.BySoftIntegration;

namespace QF.BySoft.Integration.Features.AgentOutputFile;

public class AgentOutputFileCreatedHandler : INotificationHandler<AgentOutputFileCreated>
{
    private readonly IBySoftIntegration _bySoftIntegration;
    private readonly ILogger<AgentOutputFileCreatedHandler> _logger;

    public AgentOutputFileCreatedHandler(
        ILogger<AgentOutputFileCreatedHandler> logger,
        IBySoftIntegration bySoftIntegration)
    {
        _logger = logger;
        _bySoftIntegration = bySoftIntegration;
    }

    public async Task Handle(AgentOutputFileCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("File created: {FilePath}", notification.FilePath);

        // default file creation timeout
        await Task.Delay(1500, cancellationToken);

        // define file paths
        var jsonFilePath = notification.FilePath;

        _logger.LogInformation("Start processing file: {JsonFilePath}", jsonFilePath);
        await _bySoftIntegration.HandleManufacturabilityCheckRequestAsync(jsonFilePath);
    }
}
