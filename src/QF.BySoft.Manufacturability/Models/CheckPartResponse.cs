namespace QF.BySoft.Manufacturability.Models;

public class CheckPartResponse
{
    public string Name { get; set; }
    public string State { get; set; }

    public string GeometryState { get; set; }
    public string CuttingState { get; set; }
    public string BendingState { get; set; }

    public CheckPartDetails[] GeometryDetails { get; set; }
    public CheckPartDetails[] CuttingDetails { get; set; }
    public CheckPartDetails[] BendingDetails { get; set; }
}
