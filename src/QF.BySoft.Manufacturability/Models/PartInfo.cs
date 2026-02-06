namespace QF.BySoft.Manufacturability.Models;

public class PartInfo
{
    public required string? Name { get; set; }
    public required string? Description { get; set; }
    public required string? UserInfo1 { get; set; }
    public required string? UserInfo2 { get; set; }
    // used as key
    public required string UserInfo3 { get; set; }
    public required string MaterialName { get; set; }
    public double? Thickness { get; set; }
    public required string? CuttingMachineName { get; set; }
    public required string? BendingMachineName { get; set; }
}
