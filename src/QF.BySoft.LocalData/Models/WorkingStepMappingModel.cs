namespace QF.BySoft.LocalData.Models;

public class WorkingStepMappingModel
{
    /// <summary>Column 1 – the QuotationFactory working step code (e.g. "SheetCutting", "SheetBending").</summary>
    public required string WorkingStepCode { get; set; }

    /// <summary>Column 2 – optional resource / machine id. Empty means "any resource".</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Column 3 – the custom working step code to map to.</summary>
    public required string CustomWorkingStepCode { get; set; }
}

