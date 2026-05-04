using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QF.BySoft.Entities.Repositories;
using QF.BySoft.LocalData.Helpers;

namespace QF.BySoft.LocalData;

public class FeaturesMappingRepository: IFeaturesMappingRepository
{
    private const string MappingFileName = "FeaturesMapping.xlsx";

    private readonly string _applicationBasePath = ApplicationInfo.GetApplicationBasePath();
    public string GetCustomFeatureDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(description));
        }

        var filePathName = Path.Combine(_applicationBasePath, "data", MappingFileName);

        if (!File.Exists(filePathName))
        {
            throw new Exception($"Data/{MappingFileName} not found. Expected at: {filePathName}");
        }

        XLWorkbook workbook;
        using (var fileStream = new FileStream(filePathName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            workbook = new XLWorkbook(fileStream);
        }

        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            throw new ApplicationException("FeaturesMappingRepository no worksheet found");
        }

        // select range with used data in the sheet
        var rangeUsed = worksheet.RangeUsed();

        // first column has the FeatureCode
        var column1 = rangeUsed!.Column(1);
        // search for the
        var foundCells = column1.Search(description, CompareOptions.IgnoreCase);
        if (foundCells != null && foundCells.Any())
        {
            var firstCell = foundCells.FirstOrDefault();
            var rowNumber = firstCell!.Address.RowNumber;
            // get the CustomCode
            var column2 = rangeUsed.Column(2);
            var customCode = column2.Cell(rowNumber).Value.ToString();
            return customCode;
        }

        return string.Empty;
    }
}
