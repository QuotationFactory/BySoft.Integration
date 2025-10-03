using System.Threading.Tasks;
using MetalHeaven.Agent.Shared.External.Messages;

namespace QF.BySoft.Manufacturability.Interfaces;

/// <summary>
///     BySoft do manufacturability check bending
/// </summary>
public interface IBySoftManufacturabilityCheck
{
    /// <summary>
    ///     Creates the BySoft LOP file and returns the file contents as string
    /// </summary>
    /// <returns></returns>
    Task<RequestManufacturabilityCheckOfPartTypeMessageResponse> ManufacturabilityCheckCuttingAsync(RequestManufacturabilityCheckOfPartTypeMessage request, string stepFilePathName);
    Task<RequestManufacturabilityCheckOfPartTypeMessageResponse> ManufacturabilityCheckBendingAsync(RequestManufacturabilityCheckOfPartTypeMessage request, string stepFilePathName);

}
