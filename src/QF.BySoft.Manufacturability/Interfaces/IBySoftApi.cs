using System.Threading.Tasks;
using QF.BySoft.Manufacturability.Models;

namespace QF.BySoft.Manufacturability.Interfaces;

public interface IBySoftApi
{
    Task ImportPartAsync(string path, string subDirectory, bool secondTry = false);
    Task<string> GetUriFromPartNameAsync(string partName, string subDirectory);
    Task UpdatePartAsync(string partUri, string materialName, string bendingMachineName, string cuttingMachineName, double thickness);
    Task<SetTechnologyResponse> SetBendingTechnologyAsync(string partUri);
    Task DeletePartAsync(string partUri);
    Task SetCuttingTechnologyAsync(string partUri);
    Task<CheckPartResponse> CheckPartAsync(string partUri);
}
