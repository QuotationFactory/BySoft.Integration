using System.IO;
using QF.BySoft.Entities;

namespace QF.BySoft.Integration.Features.AgentOutputFile;

public static class SettingsExtension
{
    private static string GetOrCreateDirectory(string rootDirectoryPath, string subDir = "")
    {
        var totalPath = Path.Combine(rootDirectoryPath, subDir);

        if (Directory.Exists(totalPath))
        {
            return totalPath;
        }

        if (!Directory.Exists(totalPath))
        {
            Directory.CreateDirectory(totalPath);
        }

        return totalPath;
    }

    public static string GetOrCreateAgentInputDirectory(this BySoftIntegrationSettings settings, string integrationName = "", bool createIfNotExists = false)
    {
        var inputDirectory = Path.Combine(settings.RootDirectory, integrationName, "Input");
        return inputDirectory.DirectoryExistsOrCreate(createIfNotExists) ? inputDirectory : string.Empty;
    }

    public static string GetOrCreateAgentOutputDirectory(this BySoftIntegrationSettings settings, string integrationName = "", bool createIfNotExists = false)
    {
        var outputDirectory = Path.Combine(settings.RootDirectory, integrationName, "Output");
        return outputDirectory.DirectoryExistsOrCreate(createIfNotExists) ? outputDirectory : string.Empty;
    }

    public static bool DirectoryExistsOrCreate(this string directory, bool createIfNotExists = false)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        if (Directory.Exists(directory))
            return true;

        if (!createIfNotExists) return false;

        Directory.CreateDirectory(directory);

        return Directory.Exists(directory);
    }

    private static string GetIntegrationRootDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return Path.Combine(settings.RootDirectory, integrationName);
    }


    public static string GetStepDownloadDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(Path.Combine(GetIntegrationRootDirectory(settings, integrationName), "Output", "Step"));
    }

    public static string GetInputDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(Path.Combine(settings.RootDirectory, integrationName, "Input"));
    }

    public static string GetInputSendDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(Path.Combine(settings.RootDirectory, integrationName, "InputSend"));
    }

    public static string GetOutputDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(Path.Combine(settings.RootDirectory, integrationName, "Output"));
    }

    public static string GetProcessingDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(settings.GetOutputDirectory(integrationName), "Processing");
    }

    public static string GetProcessedDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(settings.GetOutputDirectory(integrationName), "Processed");
    }

    public static string GetErrorDirectory(this BySoftIntegrationSettings settings, string integrationName)
    {
        return GetOrCreateDirectory(settings.GetOutputDirectory(integrationName), "Error");
    }

    public static string MoveFileToProcessing(this BySoftIntegrationSettings settings, string integrationName, string filePath)
    {
        return filePath.MoveFileToDirectory(settings.GetProcessingDirectory(integrationName));
    }

    public static string MoveFileToProcessed(this BySoftIntegrationSettings settings, string integrationName, string filePath)
    {
        return filePath.MoveFileToDirectory(settings.GetProcessedDirectory(integrationName));
    }

    public static string MoveFileToError(this BySoftIntegrationSettings settings, string integrationName, string filePath)
    {
        return filePath.MoveFileToDirectory(settings.GetErrorDirectory(integrationName));
    }

    public static string CopyFileToProcessed(this BySoftIntegrationSettings settings, string integrationName, string filePath)
    {
        return filePath.CopyFileToDirectory(settings.GetProcessedDirectory(integrationName));
    }

    public static string CopyFileToError(this BySoftIntegrationSettings settings, string integrationName, string filePath)
    {
        return filePath.CopyFileToDirectory(settings.GetErrorDirectory(integrationName));
    }

    public static string MoveFileToAgentInput(this BySoftIntegrationSettings settings, string integrationName, string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var subDirectoryPath = settings.GetInputDirectory(integrationName);

        var destFileName = Path.Combine(subDirectoryPath, Path.GetFileName(filePath));

        File.Copy(filePath, destFileName, overwrite:true);
        File.Delete(filePath);

        return destFileName;
    }

    public static string MoveFileToDirectory(this string filePath, string directoryPath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var fileInfo = new FileInfo(filePath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var destFileName = Path.Combine(directoryPath, fileInfo.Name);
        return filePath.MoveFile(destFileName);
    }

    public static string MoveFile(this string filePath, string destinationFilePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var result = destinationFilePath;

        while (File.Exists(result))
        {
            result = Path.Combine(Path.GetDirectoryName(destinationFilePath),
                $"{Path.GetFileNameWithoutExtension(destinationFilePath)} (1){Path.GetExtension(destinationFilePath)}");
        }

        File.Copy(filePath, result, overwrite:true);
        File.Delete(filePath);

        return result;
    }


    public static string CopyFileToDirectory(this string filePath, string directoryPath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var fileInfo = new FileInfo(filePath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var destFileName = Path.Combine(directoryPath, fileInfo.Name);
        return filePath.CopyFile(destFileName);
    }

    public static string CopyFile(this string filePath, string destinationFilePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var result = destinationFilePath;

        while (File.Exists(result))
        {
            result = Path.Combine(Path.GetDirectoryName(destinationFilePath),
                $"{Path.GetFileNameWithoutExtension(destinationFilePath)} (1){Path.GetExtension(destinationFilePath)}");
        }

        File.Copy(filePath, result);

        return result;
    }
}
