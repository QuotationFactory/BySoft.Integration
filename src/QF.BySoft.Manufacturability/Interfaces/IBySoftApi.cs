using System.Threading.Tasks;
using QF.BySoft.Manufacturability.Models;

namespace QF.BySoft.Manufacturability.Interfaces;

public interface IBySoftApi
{
    Task<string?> GetUriFromPartNameAsync(string partName, string subDirectory);
    Task<PartInfo?> GetPartInfoAsync(string partUri);
    Task ImportPartAsync(string path, string subDirectory);
    Task UpdatePartAsync(string partUri, UpdatePartArgs updatePartArgs);
    Task<SetTechnologyResponse?> SetBendingTechnologyAsync(string partUri);
    Task DeletePartAsync(string partUri);
    Task SetCuttingTechnologyAsync(string partUri);
    Task<CheckPartResponse?> CheckPartAsync(string partUri);
}
