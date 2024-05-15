using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QF.BySoft.Entities.Repositories;
using QF.BySoft.LocalData.Helpers;

namespace QF.BySoft.LocalData;

public class MachineMappingRepository : IMachineMappingRepository
{
    private readonly string _applicationBasePath = ApplicationInfo.GetApplicationBasePath();

    public string GetBySoftMachineId(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(resourceId));
        }

        var filePathName = Path.Combine(_applicationBasePath, "data", "MachineMapping.xlsx");

        if (!File.Exists(filePathName))
        {
            throw new Exception($"Data/MachineMapping.xlsx not found. Expected at: {filePathName}");
        }

        XLWorkbook workbook;
        using (var fileStream = new FileStream(filePathName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            workbook = new XLWorkbook(fileStream);
        }

        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            throw new ApplicationException("MachineMappingRepository no worksheet found");
        }

        // select range with used data in the sheet
        var rangeUsed = worksheet.RangeUsed();

        // first column has the machine id's
        var column1 = rangeUsed.Column(1);
        // search for the
        var foundCells = column1.Search(resourceId, CompareOptions.IgnoreCase);
        if (foundCells != null && foundCells.Any())
        {
            var firstCell = foundCells.FirstOrDefault();
            var rowNumber = firstCell.Address.RowNumber;
            // get the BySoft machine id
            var column2 = rangeUsed.Column(2);
            var machineId = column2.Cell(rowNumber).Value.ToString();
            return machineId;
        }

        return string.Empty;
    }
}
