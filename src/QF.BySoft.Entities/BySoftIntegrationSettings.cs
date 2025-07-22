namespace QF.BySoft.Entities;

public class BySoftIntegrationSettings
{
    public string RootDirectory { get; set; }

    // Unit of measurement. Can be mm or inch
    // This is for the Thickness of the material
    public string MachineUnitOfMeasurement { get; set; }

    // If true, maps the material based on material group and keyword(s)
    // If false, maps the material based on the article ID.
    public bool MaterialMappingWithKeywords { get; set; }

    /// <summary>
    ///     Server where the BySoft CAM API is running
    /// </summary>
    public string BySoftApiServer { get; set; }

    /// <summary>
    ///     Port on which the BySoft CAM API is running. Default: 56111
    /// </summary>
    public string BySoftApiPort { get; set; }

    /// <summary>
    ///     Root path of the BySoft CAM API. Default: api/v1
    /// </summary>
    public string BySoftApiRootPath { get; set; }

    /// <summary>
    ///     If set to true, the imported part will not get deleted from BySoft
    ///     If set to false, the imported part will be deleted after the manufacturability check
    /// </summary>
    public bool SavePartInBySoft { get; set; }

    /// <summary>
    ///     If set to false, the step file will have the name partId.step,
    ///     If set to true, the step file will have the name partId_partName.step
    /// </summary>
    public bool SavePartWithCombinedFileName { get; set; }

    /// <summary>
    ///     If set to true, will set the cutting technology.
    ///     Attention: we do not return the result whether it is manufacturable or not.
    /// </summary>
    public bool SetCuttingTechnology { get; set; }

    /// <summary>
    ///     Cutting machine name used before setting the cutting technology
    /// </summary>
    public string CuttingMachineName { get; set; }

    /// <summary>
    ///     Some warnings should be seen as errors
    /// </summary>
    public string[] WarningsAsErrors { get; set; } = [];

    public int NumberOfConcurrentTasks { get; set; }

    /// <summary>
    /// The Rotation allowance for parts without bending can be configured.
    /// Default value is 1 degree.
    /// </summary>
    public int RotationAllowancePartWithoutBending { get; set; } = 1;
    /// <summary>
    /// The Rotation allowance for parts with bending can be configured.
    /// Default value is 90 degrees.
    /// </summary>
    public int RotationAllowancePartWithBending { get; set; } = 90;
    /// <summary>
    /// The Rotation allowance for parts with surface treatment can be configured.
    /// Default value is 180 degrees.
    /// </summary>
    public int RotationAllowancePartWithSurfaceTreatment { get; set; } = 180;
    /// <summary>
    /// This is the list of keywords that are used to identify that the material used contains surface treatment.
    /// we recommend to use keywords like
    /// "laser foil", "double foil", "mirror 8", "5wl", "6wl", "cd-overlay", "circle polished", "centerless grinded", "steel look"
    /// "varnished", "leather structure","stucco design","rice grain structure", "centerless grinded", "one-sided grinding","double-sided grinding"
    /// </summary>
    public string [] KeywordsForPartWithSurfaceTreatment { get; set; } =
    ["laser foil", "double foil", "mirror 8", "5wl", "6wl", "cd-overlay", "circle polished", "centerless grinded", "steel look",
        "varnished", "leather structure", "stucco design", "rice grain structure", "centerless grinded", "one-sided grinding", "double-sided grinding"];
}
