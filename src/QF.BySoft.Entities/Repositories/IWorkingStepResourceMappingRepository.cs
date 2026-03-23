namespace QF.BySoft.Entities.Repositories;

public interface IWorkingStepResourceMappingRepository
{
    /// <summary>
    /// Returns the custom working step code for the given working step type and optional resource id.
    /// When a resource-specific mapping exists it takes priority over a general (no resourceId) mapping.
    /// Returns an empty string when no mapping is found.
    /// </summary>
    string GetCustomWorkingStepCode(string workingStepType, string resourceId);
}

