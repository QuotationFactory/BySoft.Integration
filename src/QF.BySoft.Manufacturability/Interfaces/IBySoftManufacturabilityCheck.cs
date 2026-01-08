using System.Threading.Tasks;
using MetalHeaven.Agent.Shared.External.Messages;

namespace QF.BySoft.Manufacturability.Interfaces;

/// <summary>
///     BySoft do manufacturability check bending
/// </summary>
public interface IBySoftManufacturabilityCheck
{
    Task<RequestManufacturabilityCheckOfPartTypeMessageResponse> ManufacturabilityCheckAsync(RequestManufacturabilityCheckOfPartTypeMessage request, string geometryFilePathName);
}
