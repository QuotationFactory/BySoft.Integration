namespace QF.BySoft.Manufacturability.Models;

public class UpdatePartInfo
{
    // public string Name { get; set; }
    // public string Description { get; set; }
    // public string UserInfo01 { get; set; }
    // public string UserInfo02 { get; set; }
    // public string UserInfo03 { get; set; }
    public string MaterialName { get; set; }
    public string CuttingMachineName { get; set; }
    public string BendingMachineName { get; set; }
    public double Thickness { get; set; }
    public string Priority { get; set; }
}
