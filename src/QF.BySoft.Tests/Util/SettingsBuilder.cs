using QF.BySoft.Entities;

namespace QF.BySoft.Tests.Util;

public static class SettingsBuilder
{
    public static BySoftIntegrationSettings GetBySoftIntegrationSettings()
    {
        var bySoftIntegrationSettings = new BySoftIntegrationSettings
        {
            MachineUnitOfMeasurement = "mm",
            MaterialMappingWithKeywords = true,
            BySoftApiServer = "http://localhost",
            BySoftApiPort = "12345",
            BySoftApiRootPath = "api/v1",
            SavePartInBySoft = false,
            SavePartWithCombinedFileName = false
        };
        return bySoftIntegrationSettings;
    }
}
