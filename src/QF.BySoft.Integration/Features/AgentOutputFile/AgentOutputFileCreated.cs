namespace QF.BySoft.Integration.Features.AgentOutputFile;

public class AgentOutputFileCreated
{
    public AgentOutputFileCreated(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}
