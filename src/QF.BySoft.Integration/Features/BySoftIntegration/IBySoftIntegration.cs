using System.Threading.Tasks;

namespace QF.BySoft.Integration.Features.BySoftIntegration;

/// <summary>
///     BySoft import that will be executed in the agent (local client)
/// </summary>
public interface IBySoftIntegration
{
    Task HandleManufacturabilityCheckRequestAsync(string jsonFilePath);
}
