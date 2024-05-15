using MediatR;

namespace QF.BySoft.Integration.Features.AgentOutputFile;

public class AgentOutputFileCreated : INotification
{
    public AgentOutputFileCreated(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}
