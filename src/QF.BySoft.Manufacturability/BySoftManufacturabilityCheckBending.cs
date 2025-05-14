using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MetalHeaven.Agent.Shared.External.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QF.BySoft.Entities;
using QF.BySoft.Entities.Repositories;
using QF.BySoft.Manufacturability.Interfaces;
using QF.BySoft.Manufacturability.Models;
using Versioned.ExternalDataContracts.Contracts.Resource;
using Versioned.ExternalDataContracts.Enums;

namespace QF.BySoft.Manufacturability;

public class BySoftManufacturabilityCheckBending : IBySoftManufacturabilityCheckBending
{
    // Machine unit of measurement, important for the Thickness of the material
    private const string MachineUnitOfMeasurementMillimeters = "mm";
    private const string MachineUnitOfMeasurementInch = "inch";

    // BySoft constants
    private const string BySoftResponseOk = "Ok";
    private readonly IBySoftApi _bySoftApi;

    private readonly BySoftIntegrationSettings _bySoftIntegrationSettings;
    private readonly ILogger<BySoftManufacturabilityCheckBending> _logger;
    private readonly IMachineMappingRepository _machineMappingRepository;
    private readonly IMaterialMappingRepository _materialMappingRepository;

    public BySoftManufacturabilityCheckBending(
        IOptions<BySoftIntegrationSettings> bySoftIntegrationSettings,
        IMachineMappingRepository machineMappingRepository,
        IMaterialMappingRepository materialMappingRepository,
        ILogger<BySoftManufacturabilityCheckBending> logger,
        IBySoftApi bySoftApi
    )
    {
        _bySoftIntegrationSettings = bySoftIntegrationSettings.Value;
        _machineMappingRepository = machineMappingRepository;
        _materialMappingRepository = materialMappingRepository;
        _logger = logger;
        _bySoftApi = bySoftApi;
    }

    public async Task<RequestManufacturabilityCheckOfPartTypeMessageResponse> ManufacturabilityCheckBendingAsync(
        RequestManufacturabilityCheckOfPartTypeMessage request, string stepFilePathName)
    {
        CheckApiSettings();
        // Retrieve from the request object
        var materialName = GetMaterialName(request);
        var bendingMachineName = GetBendingMachineName(request);
        var cuttingMachineName = _bySoftIntegrationSettings.CuttingMachineName;
        var thickness = GetThickness(request);
        // TODO: should this be an app setting??
        const string subDirectory = "manufacturability-check";
        var partName = Path.GetFileNameWithoutExtension(stepFilePathName);

        // 0. Delete part if it already exists
        await DeleteExistingPartAsync(partName, subDirectory);

        // 1. Create part from file
        await _bySoftApi.ImportPartAsync(stepFilePathName, subDirectory);

        // 2. Get the Uri of the part
        var partUri = await _bySoftApi.GetUriFromPartNameAsync(partName, subDirectory);
        if (string.IsNullOrWhiteSpace(partUri))
        {
            throw new ArgumentException($"Part name could not be retrieved. Part name: {partName}");
        }

        // 3. Update part with material and machine info
        await _bySoftApi.UpdatePartAsync(partUri, materialName, bendingMachineName, cuttingMachineName, thickness);

        // 4. Add Bending technology => This is also the *initial* check of manufacturability
        await _bySoftApi.SetBendingTechnologyAsync(partUri);

        // 4b. Set the cutting technology => We do not check the manufacturability of this
        // We only set it, for customers that save the part in the database
        if (_bySoftIntegrationSettings.SetCuttingTechnology)
        {
            await _bySoftApi.SetCuttingTechnologyAsync(partUri);
        }

        // 4c. Check part for manufacturability states
        var checkPartResult = await _bySoftApi.CheckPartAsync(partUri);

        // 5. Delete part
        if (!_bySoftIntegrationSettings.SavePartInBySoft)
        {
            await _bySoftApi.DeletePartAsync(partUri);
        }

        // 6. Create response
        var response = CreateResponse(request, checkPartResult);

        return response;
    }

    private async Task DeleteExistingPartAsync(string partName, string subDirectory)
    {
        _logger.LogDebug("DeleteExistingPart. PartName: {PartName}", partName);
        var partUri = await _bySoftApi.GetUriFromPartNameAsync(partName, subDirectory);
        if (!string.IsNullOrWhiteSpace(partUri))
        {
            _logger.LogDebug("Part exists. Delete it.");
            await _bySoftApi.DeletePartAsync(partUri);
        }
    }

    private void CheckApiSettings()
    {
        _logger.LogDebug("CheckApiSettings");

        if (string.IsNullOrWhiteSpace(_bySoftIntegrationSettings.BySoftApiServer))
        {
            throw new ApplicationException("No BySoft API server provided in the app.settings");
        }

        if (string.IsNullOrWhiteSpace(_bySoftIntegrationSettings.BySoftApiPort))
        {
            throw new ApplicationException("No BySoft API port provided in the app.settings");
        }

        if (string.IsNullOrWhiteSpace(_bySoftIntegrationSettings.BySoftApiRootPath))
        {
            throw new ApplicationException("No BySoft API Root Path provided in the app.settings");
        }
    }

    #region PrivateHelpers

    private string GetMaterialName(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        var useMaterialMappingWithKeywords = _bySoftIntegrationSettings.MaterialMappingWithKeywords;

        if (useMaterialMappingWithKeywords)
        {
            var materialKeywords = request.PartType.Material?.SelectableArticles?.SelectedArticle?.Tokens;
            if (materialKeywords == null || materialKeywords.Count == 0)
            {
                // Material not found in the repository, throw error
                throw new ApplicationException(
                    "MaterialKeywords are not set");
            }

            var integrationMaterialId = _materialMappingRepository.GetMaterialCodeFromKeywords(materialKeywords);
            if (string.IsNullOrWhiteSpace(integrationMaterialId))
            {
                // Material not found in the repository, throw error
                throw new ApplicationException(
                    $"Material mapping not found in data/MaterialMapping.xlsx for material-id: {string.Join(", ", materialKeywords)}");
            }

            return integrationMaterialId;
        }
        else
        {
            var materialId = request.PartType.Material.SelectableArticles.SelectedArticle.Id;
            var integrationMaterialId = _materialMappingRepository.GetMaterialCodeFromArticle(materialId);
            if (string.IsNullOrWhiteSpace(integrationMaterialId))
            {
                // Material not found in the repository, throw error
                throw new ApplicationException(
                    $"Material mapping not found in data/MaterialMapping.xlsx for material-id: {materialId}.");
            }

            return integrationMaterialId;
        }
    }

    private string GetBendingMachineName(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        // Per request/part there is only one bending machine
        // But there can be multiple machines defined in quotation factory and the intergration.
        // We need to find the machine used in quotation factory, then find the corresponding machine id of the integration
        string resourceId = null;
        foreach (var estimation in request.PartType.Estimations)
        {
            foreach (var stepEstimation in estimation.WorkingStepEstimations)
            {
                if (
                    stepEstimation.ResourceWorkingStepKey.WorkingStepKey.PrimaryWorkingStep == WorkingStepTypeV1.SheetBending &&
                    stepEstimation.ResourceWorkingStepKey.WorkingStepKey.SecondaryWorkingStep == WorkingStepTypeV1.SheetBending
                )
                {
                    resourceId = stepEstimation.ResourceWorkingStepKey.ResourceId.ToString();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            // no machine found for bending, throw error without machines and log error
            throw new ApplicationException(
                $"No bending machine found in request of project-id: {request.ProjectId}, part-id: {request.PartType.Id}");
        }

        var machineId = _machineMappingRepository.GetBySoftMachineId(resourceId);

        if (string.IsNullOrWhiteSpace(machineId))
        {
            throw new ApplicationException(
                $"Machine could not be found in data/MachineMapping.xlsx. for machine-id: {resourceId}.");
        }

        return machineId;
    }

    private double GetThickness(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        double thickness = 0;
        if (request.PartType.Material.SelectableArticles.SelectedArticle.Dimensions.Thickness.HasValue)
        {
            // Thickness should be provided in the units of the machine.
            // Get the unit of measurement from the app.settings
            switch (_bySoftIntegrationSettings.MachineUnitOfMeasurement)
            {
                case MachineUnitOfMeasurementInch:
                    thickness = request.PartType.Material.SelectableArticles.SelectedArticle.Dimensions.Thickness.Value.Inches;
                    break;
                case MachineUnitOfMeasurementMillimeters:
                    thickness = request.PartType.Material.SelectableArticles.SelectedArticle.Dimensions.Thickness.Value.Millimeters;
                    break;
                default:
                    // throw error and do not provide thickness, as we could not determine the units of measurement
                    throw new ApplicationException(
                        "Machine units of measurement not provided correctly in the app.settings. Possible values are: mm or inch");
            }
        }

        if (thickness == 0)
        {
            throw new ApplicationException("Thickness of material not found in the request.");
        }

        return thickness;
    }

    private RequestManufacturabilityCheckOfPartTypeMessageResponse CreateResponse(
        RequestManufacturabilityCheckOfPartTypeMessage request,
        CheckPartResponse checkPartResult)
    {
        // If response is Ok WarningFound or it is manufacturable, else not.
        var isManufacturable = !string.Equals(checkPartResult.State, "ErrorFound", StringComparison.OrdinalIgnoreCase);

        if (_bySoftIntegrationSettings.WarningsAsErrors.Any())
        {
            var warningsAsErrors = _bySoftIntegrationSettings.WarningsAsErrors.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (checkPartResult.BendingDetails?.Any(x => warningsAsErrors.Contains(x.Description)) ?? false)
            {
                isManufacturable = false;
            }

            if (checkPartResult.CuttingDetails?.Any(x => warningsAsErrors.Contains(x.Description)) ?? false)
            {
                isManufacturable = false;
            }

            if (checkPartResult.GeometryDetails?.Any(x => warningsAsErrors.Contains(x.Description)) ?? false)
            {
                isManufacturable = false;
            }
        }


        return new RequestManufacturabilityCheckOfPartTypeMessageResponse
        {
            ProjectId = request.ProjectId,
            PartTypeId = request.PartType.Id,
            WorkingStepKey = new WorkingStepKeyV1(
                WorkingStepTypeV1.SheetBending,
                WorkingStepTypeV1.SheetBending),
            IsManufacturable = isManufacturable,
            EventLogs = BuildEventLog(request, checkPartResult)
        };
    }

    private static EventLogLevel GetLogLevel(string bysoftState)
    {
        if (string.IsNullOrWhiteSpace(bysoftState))
        {
            return EventLogLevel.Information;
        }

        return bysoftState.ToLowerInvariant() switch
        {
            "errorfound" => EventLogLevel.Error,
            "warningfound" => EventLogLevel.Warning,
            _ => EventLogLevel.Information
        };
    }

    private static List<EventLog> BuildEventLog(RequestManufacturabilityCheckOfPartTypeMessage request, CheckPartResponse checkPartResult)
    {
        var logs = new List<EventLog>();

        if (checkPartResult.BendingDetails != null && checkPartResult.BendingDetails.Any())
        {
            logs.AddRange(checkPartResult.BendingDetails.Select(bendingDetail => new EventLog
            {
                DateTime = DateTime.UtcNow,
                Message = $"{bendingDetail.Description}. Status: {bendingDetail.State}",
                Level = GetLogLevel(bendingDetail.State),
                ProjectId = request.ProjectId,
                PartTypeId = request.PartType.Id
            }));
        }

        if (checkPartResult.CuttingDetails != null && checkPartResult.CuttingDetails.Any())
        {
            logs.AddRange(checkPartResult.CuttingDetails.Select(cuttingDetail => new EventLog
            {
                DateTime = DateTime.UtcNow,
                Message = $"{cuttingDetail.Description}. Status: {cuttingDetail.State}",
                Level = GetLogLevel(cuttingDetail.State),
                ProjectId = request.ProjectId,
                PartTypeId = request.PartType.Id
            }));
        }

        if (checkPartResult.GeometryDetails != null && checkPartResult.GeometryDetails.Any())
        {
            logs.AddRange(checkPartResult.GeometryDetails.Select(geometryDetail => new EventLog
            {
                DateTime = DateTime.UtcNow,
                Message = $"{geometryDetail.Description}. Status: {geometryDetail.State}",
                Level = GetLogLevel(geometryDetail.State),
                ProjectId = request.ProjectId,
                PartTypeId = request.PartType.Id
            }));
        }

        return logs;
    }

    #endregion
}
