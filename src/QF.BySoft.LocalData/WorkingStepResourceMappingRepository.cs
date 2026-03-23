using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QF.BySoft.Entities.Repositories;
using QF.BySoft.LocalData.Helpers;
using QF.BySoft.LocalData.Models;

namespace QF.BySoft.LocalData;

public class WorkingStepResourceMappingRepository : IWorkingStepResourceMappingRepository
{
    private const string MappingFileName = "WorkingStepResourceMapping.xlsx";

    private readonly string _applicationBasePath = ApplicationInfo.GetApplicationBasePath();

    public string GetCustomWorkingStepCode(string workingStepType, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(workingStepType))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(workingStepType));
        }

        var mappings = LoadMappings();

        // Prefer a resource-specific match; fall back to a general row (empty resourceId).
        var resourceSpecific = mappings
            .FirstOrDefault(m =>
                string.Equals(m.WorkingStepCode, workingStepType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase));

        if (resourceSpecific != null)
        {
            return resourceSpecific.CustomWorkingStepCode;
        }

        var general = mappings
            .FirstOrDefault(m =>
                string.Equals(m.WorkingStepCode, workingStepType, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(m.ResourceId));

        return general?.CustomWorkingStepCode ?? string.Empty;
    }

    private IReadOnlyList<WorkingStepMappingModel> LoadMappings()
    {
        var filePathName = Path.Combine(_applicationBasePath, "data", MappingFileName);

        if (!File.Exists(filePathName))
        {
            throw new FileNotFoundException(
                $"Data/{MappingFileName} not found. Expected at: {filePathName}", filePathName);
        }

        XLWorkbook workbook;
        using (var fileStream = new FileStream(filePathName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            workbook = new XLWorkbook(fileStream);
        }

        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException($"{MappingFileName}: no worksheet found.");

        var rangeUsed = worksheet.RangeUsed()
            ?? throw new InvalidOperationException($"{MappingFileName}: worksheet is empty.");

        const int colWorkingStepCode    = 1;
        const int colResourceId         = 2;
        const int colCustomWorkingStep  = 3;

        // Skip the header row (AsTable().DataRange) and require at least col 1 and col 3 to have a value.
        return rangeUsed.AsTable().DataRange.Rows()
            .Where(row =>
                row.Cell(colWorkingStepCode).GetString().Trim().Length > 0 &&
                row.Cell(colCustomWorkingStep).GetString().Trim().Length > 0)
            .Select(row => new WorkingStepMappingModel
            {
                WorkingStepCode       = row.Cell(colWorkingStepCode).GetString().Trim(),
                ResourceId            = row.Cell(colResourceId).GetString().Trim(),
                CustomWorkingStepCode = row.Cell(colCustomWorkingStep).GetString().Trim()
            })
            .ToList();
    }
}

