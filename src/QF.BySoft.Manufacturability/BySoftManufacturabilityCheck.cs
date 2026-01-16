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
using Versioned.ExternalDataContracts.Contracts.BoM;
using Versioned.ExternalDataContracts.Enums;

namespace QF.BySoft.Manufacturability;

public class BySoftManufacturabilityCheck : IBySoftManufacturabilityCheck
{
    // Machine unit of measurement, important for the Thickness of the material
    private const string MachineUnitOfMeasurementMillimeters = "mm";
    private const string MachineUnitOfMeasurementInch = "inch";

    // BySoft constants
    private const string BySoftResponseOk = "Ok";
    private readonly IBySoftApi _bySoftApi;

    private readonly BySoftIntegrationSettings _bySoftIntegrationSettings;
    private readonly ILogger<BySoftManufacturabilityCheck> _logger;
    private readonly IMachineMappingRepository _machineMappingRepository;
    private readonly IMaterialMappingRepository _materialMappingRepository;
    private readonly string[] _warningsAsErrors;

    public BySoftManufacturabilityCheck(
        IOptions<BySoftIntegrationSettings> bySoftIntegrationSettings,
        IMachineMappingRepository machineMappingRepository,
        IMaterialMappingRepository materialMappingRepository,
        ILogger<BySoftManufacturabilityCheck> logger,
        IBySoftApi bySoftApi
    )
    {
        _bySoftIntegrationSettings = bySoftIntegrationSettings.Value;
        _machineMappingRepository = machineMappingRepository;
        _materialMappingRepository = materialMappingRepository;
        _logger = logger;
        _bySoftApi = bySoftApi;
        _warningsAsErrors = _bySoftIntegrationSettings.WarningsAsErrors.ToHashSet(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<RequestManufacturabilityCheckOfPartTypeMessageResponse> ManufacturabilityCheckAsync(RequestManufacturabilityCheckOfPartTypeMessage request, string geometryDownloadFilePath)
    {
        // 0. Rename geometry file based on settings
        var geometryFilePath = MoveAndRenameGeometryFile(geometryDownloadFilePath, request);
        var partName = Path.GetFileNameWithoutExtension(geometryFilePath);
        var subDirectory = GetSubDirectory(request);

        // 1. Delete part if it already exists, only when we do not save parts in BySoft
        await DeleteExistingPartAsync(partName, subDirectory);

        // 2. Create part from file
        await _bySoftApi.ImportPartAsync(geometryFilePath, subDirectory);

        // 3. Get the Uri of the part
        var partUri = await _bySoftApi.GetUriFromPartNameAsync(partName, subDirectory);
        if (string.IsNullOrWhiteSpace(partUri))
        {
            throw new ArgumentException($"Part name could not be retrieved. Part name: {partName}");
        }

        // 4. Update part with material and machine info
        var args = CreateUpdatePartArgs(request);
        await _bySoftApi.UpdatePartAsync(partUri, args);

        // 5. Add Bending technology => This is also the *initial* check of manufacturability
        if (HasBendingActivity(request))
        {
            await _bySoftApi.SetBendingTechnologyAsync(partUri);
        }

        // 6. Set the cutting technology
        await _bySoftApi.SetCuttingTechnologyAsync(partUri);

        // 7. Check part for manufacturability states
        var checkPartResult = await _bySoftApi.CheckPartAsync(partUri);

        // 8. Delete part
        if (!_bySoftIntegrationSettings.SavePartInBySoft)
        {
            await _bySoftApi.DeletePartAsync(partUri);
        }

        // 9. Create response
        var response = CreateResponse(request, checkPartResult);

        return response;
    }

    private UpdatePartArgs CreateUpdatePartArgs(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        // Retrieve from the request object
        var materialName = GetMaterialName(request);
        var bendingMachineName = HasBendingActivity(request) ? GetBendingMachineName(request): string.Empty;
        var thickness = GetThickness(request);
        var rotationAllowance = GetRotationAllowance(request.PartType);
        var description = GetDescription(request);
        var cuttingMachineName = string.IsNullOrEmpty(_bySoftIntegrationSettings.CuttingMachineName)
            ? GetCuttingMachineName(request)
            : _bySoftIntegrationSettings.CuttingMachineName;
        var userInfo1 = GetUserInfo1(request);
        var userInfo2 = GetUserInfo2(request);
        var userInfo3 = GetUserInfo3(request);
        const int priority = 1;

       var args = new UpdatePartArgs()
        {
            Description = description,
            MaterialName = materialName,
            BendingMachineName = bendingMachineName,
            CuttingMachineName = cuttingMachineName,
            Thickness = thickness,
            RotationAllowance = rotationAllowance,
            UserInfo1 = userInfo1,
            UserInfo2 = userInfo2,
            UserInfo3 = userInfo3,
            Priority = priority
        };
        return args;
    }

    private static bool HasBendingActivity(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        return request.PartType.Activities.Any(x => x.WorkingStepType == WorkingStepTypeV1.SheetBending);
    }

    private string MoveAndRenameGeometryFile(string geometryFilePath, RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        var originalFileNameExtension = Path.GetExtension(request.PartType.OriginalFileName);
        string inputFileName;
        if (string.IsNullOrWhiteSpace(_bySoftIntegrationSettings.DefaultGeometryFileNameFormat))
        {
            // The name of the step-file depends on the app setting SavePartWithCombinedFileName
            // If false: Step file name will have the same name as the part-id , with the extension .step
            // If true : Step file name will be partId_partName , with the extension .step
            var originalFileName = request.PartType.AssemblyId == Constants.AssemblyIdRepresentingIndividualParts
                ? request.PartType.OriginalFileName
                : $"{request.PartType.Name}{originalFileNameExtension}";
            var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            // Replace invalid file name characters
            originalFileNameWithoutExtension = Path.GetInvalidFileNameChars().Aggregate(originalFileNameWithoutExtension, (current, invalidChar) => current.Replace(invalidChar, '_'));

            inputFileName = _bySoftIntegrationSettings.SavePartWithCombinedFileName
                ? $"{request.PartType.Id.ToString()}_{originalFileNameWithoutExtension}{originalFileNameExtension}"
                : $"{request.PartType.Id.ToString()}{originalFileNameExtension}";
        }
        else
        {
            inputFileName = GeometryNameReplacer(_bySoftIntegrationSettings.DefaultGeometryFileNameFormat, request, originalFileNameExtension);
        }

        var movedFilePath = Path.GetDirectoryName(geometryFilePath);
        var destFilePath = Path.Combine(movedFilePath ?? string.Empty, inputFileName);
        _logger.LogInformation("move geometry file from {GeometryFilePath} to {DestFilePath}", geometryFilePath, destFilePath);
        File.Move(geometryFilePath, destFilePath, true);

        return destFilePath;
    }

    private string GetDescription(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        // Example: Order number:'ORD-123' Reference:'REF-456' PartName: 'Bracket' RowNumber:'1'
        // here we can add a function to use variable substitution defined in app settings to create a custom description
        // if no variable substitution is defined, we use the default format

        if (_bySoftIntegrationSettings.DefaultDescriptionFieldFormat == null)
        {
            return null;
        }

        if (_bySoftIntegrationSettings.DefaultDescriptionFieldFormat.Equals("[Default]", StringComparison.OrdinalIgnoreCase))
        {
            return $"Order number:'{request.OrderNumber}' Reference:'{request.ProjectReference}' PartName: '{request.PartType.Name}' RowNumber:'{request.PartType.RowNumber}'";
        }

        var description = _bySoftIntegrationSettings.DefaultDescriptionFieldFormat;
        description = Replacer(description, request);
        return description;
    }

    private string GetUserInfo1(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        if (_bySoftIntegrationSettings.DefaultInfo1FieldFormat == null)
        {
            return null;
        }

        var info1 = _bySoftIntegrationSettings.DefaultInfo1FieldFormat;
        info1 = Replacer(info1, request);
        return info1;
    }

    private string GetUserInfo2(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        if (_bySoftIntegrationSettings.DefaultInfo2FieldFormat == null)
        {
            return null;
        }

        var info2 = _bySoftIntegrationSettings.DefaultInfo2FieldFormat;
        info2 = Replacer(info2, request);
        return info2;
    }

    private string GetUserInfo3(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        if (_bySoftIntegrationSettings.DefaultInfo3FieldFormat == null)
        {
            return null;
        }

        var info3 = _bySoftIntegrationSettings.DefaultInfo3FieldFormat;
        info3 = Replacer(info3, request);
        return info3;
    }

    private static string Replacer(string input, RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        input = input.Replace("{BuyingPartyName}", request.BuyingPartyName);
        input = input.Replace("{BuyingPartyCode}", request.BuyingPartyCode);
        input = input.Replace("{ProjectId}", request.ProjectId.ToString());
        input = input.Replace("{PartTypeId}", request.PartType.Id.ToString());
        input = input.Replace("{OrderNumber}", request.OrderNumber ?? string.Empty);
        input = input.Replace("{ProjectReference}", request.ProjectReference ?? string.Empty);
        input = input.Replace("{PartName}", request.PartType.Name);
        input = input.Replace("{RowNumber}", request.PartType.RowNumber.ToString());

        return input;
    }

    private static string GeometryNameReplacer(string geometryName, RequestManufacturabilityCheckOfPartTypeMessage request, string originalFileNameExtension)
    {
        // The name of the step-file depends on the app setting SavePartWithCombinedFileName
        // If false: Step file name will have the same name as the part-id , with the extension .step
        // If true : Step file name will be partId_partName , with the extension .step
        var originalFileNameWithExtension = request.PartType.AssemblyId == Constants.AssemblyIdRepresentingIndividualParts
            ? request.PartType.OriginalFileName
            : $"{request.PartType.Name}{originalFileNameExtension}";
        var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileNameWithExtension);
        // Replace invalid file name characters
        originalFileNameWithoutExtension = Path.GetInvalidFileNameChars().Aggregate(originalFileNameWithoutExtension, (current, invalidChar) => current.Replace(invalidChar, '_'));
        originalFileNameWithExtension = Path.GetInvalidFileNameChars().Aggregate(originalFileNameWithoutExtension, (current, invalidChar) => current.Replace(invalidChar, '_'));

        var resultGeometryName = geometryName.Replace("{originalFileNameWithExtension}", originalFileNameWithExtension);
        resultGeometryName = resultGeometryName.Replace("{originalFileNameWithoutExtension}", originalFileNameWithoutExtension);
        resultGeometryName = resultGeometryName.Replace("{originalFileNameExtension}", originalFileNameExtension);

        // here we replace the other variables except
        geometryName = Replacer(resultGeometryName, request);
        return geometryName;
    }

    private string GetSubDirectory(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        // Default sub directory failback to "manufacturability-check"
        // we build the sub directory based on the configuration
        // be careful with invalid path characters in the sub directory parts
        // BySoftApi fails or has a bug when a '.' is in the path at the end of a folder name
        // so we remove those from the buying party name, order number and project reference
        var resultDirectory = _bySoftIntegrationSettings.DefaultApiSubDirectory ?? "manufacturability-check";
        var buyingPartyNameSanitized = request.BuyingPartyName.Replace(".", "");
        var orderNumberSanitized = request.OrderNumber?.Replace(".", "") ?? string.Empty;
        var projectReferenceSanitized = request.ProjectReference?.Replace(".", "") ?? string.Empty;

        switch (_bySoftIntegrationSettings.ApiSubDirectoryConfig)
        {
            case ApiSubDirectory.Default:
                resultDirectory = _bySoftIntegrationSettings.DefaultApiSubDirectory ?? "manufacturability-check";
                break;
            case ApiSubDirectory.None:
                resultDirectory = string.Empty;
                break;
            case ApiSubDirectory.BuyingPartyName:
                resultDirectory = Path.Combine(resultDirectory, buyingPartyNameSanitized);
                break;
            case ApiSubDirectory.BuyingPartyOrderNumber:
                resultDirectory = Path.Combine(resultDirectory, buyingPartyNameSanitized, orderNumberSanitized);
                break;
            case ApiSubDirectory.BuyingPartyReference:
                resultDirectory = Path.Combine(resultDirectory, buyingPartyNameSanitized, projectReferenceSanitized);
                break;
            case ApiSubDirectory.BuyingPartyProjectId:
                resultDirectory = Path.Combine(resultDirectory, buyingPartyNameSanitized, request.ProjectId.ToString());
                break;
            case ApiSubDirectory.BuyingCodeProjectId:
                var code = string.IsNullOrWhiteSpace(request.BuyingPartyCode) ? buyingPartyNameSanitized : request.BuyingPartyCode;
                resultDirectory = Path.Combine(resultDirectory, code, request.ProjectId.ToString());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        resultDirectory = Path.GetInvalidPathChars().Aggregate(resultDirectory, (current, invalidChar) => current.Replace(invalidChar, '_'));

        return resultDirectory;
    }

    private int? GetRotationAllowance(PartTypeV1 partType)
    {
        // when part has no sheet cutting activities we do not set rotation allowance
        if (partType.Activities.All(x => x.WorkingStepType is not WorkingStepTypeV1.SheetCutting))
        {
            return null;
        }

        // RotationAllowance is a property of the part type, which is used to determine if the part can be rotated during nesting
        // when the PartType.NestingDirection is X or Y, then we expect this as a user defined value
        // we handle this as if it is a rotation allowance for the part with surface treatment
        if (partType.NestingDirection is NestingDirectionV1.X or NestingDirectionV1.Y)
        {
            return _bySoftIntegrationSettings.RotationAllowancePartWithSurfaceTreatment;
        }

        var keywords = _bySoftIntegrationSettings.KeywordsForPartWithSurfaceTreatment;
        var partHasSurfaceTreatment = partType.Material.SelectableArticles.SelectedArticle?.Tokens
            .Any(x => keywords.Contains(x, StringComparer.OrdinalIgnoreCase)) ?? false;
        if(partHasSurfaceTreatment)
        {
            // If the part has surface treatment, we use the rotation allowance for parts with surface treatment
            return _bySoftIntegrationSettings.RotationAllowancePartWithSurfaceTreatment;
        }

        // If the part has bending activities, we use the rotation allowance for parts with bending
        var hasBendingActivities = partType.Activities.Any(x => x.WorkingStepType is WorkingStepTypeV1.SheetBending);
        return hasBendingActivities ? _bySoftIntegrationSettings.RotationAllowancePartWithBending : _bySoftIntegrationSettings.RotationAllowancePartWithoutBending;
    }

    private async Task DeleteExistingPartAsync(string partName, string subDirectory)
    {
        _logger.LogDebug("DeleteExistingPart. PartName: {PartName}", partName);
        var partUri = await _bySoftApi.GetUriFromPartNameAsync(partName, subDirectory);
        if (!string.IsNullOrWhiteSpace(partUri))
        {
            _logger.LogDebug("Part exists. Delete it");
            await _bySoftApi.DeletePartAsync(partUri);
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
            var materialId = request.PartType.Material.SelectableArticles.SelectedArticle?.Id;
            if (string.IsNullOrWhiteSpace(materialId))
            {
                // Material not set, throw error
                throw new ApplicationException(
                    "MaterialId is not set");
            }

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

        var sheetCuttingWorkingStep = request.PartType.Estimations
            .SelectMany(x => x.WorkingStepEstimations
                .Where(est => est.ResourceWorkingStepKey.WorkingStepKey is { PrimaryWorkingStep: WorkingStepTypeV1.SheetBending, SecondaryWorkingStep: WorkingStepTypeV1.SheetBending }))
            .FirstOrDefault();
        var resourceId = sheetCuttingWorkingStep?.ResourceWorkingStepKey.ResourceId.ToString();

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            // no machine found for bending, throw error without machines and log error
            throw new ApplicationException(
                $"No bending machine found in request of project-id: {request.ProjectId}, part-id: {request.PartType.Id}");
        }

        var bendingMachineName = _machineMappingRepository.GetBySoftMachineId(resourceId);

        if (string.IsNullOrWhiteSpace(bendingMachineName))
        {
            throw new ApplicationException(
                $"Machine could not be found in data/MachineMapping.xlsx. for machine-id: {resourceId}.");
        }

        return bendingMachineName;
    }

    private string GetCuttingMachineName(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        // Per request/part there is only one bending machine
        // But there can be multiple machines defined in quotation factory and the integration.
        // We need to find the machine used in quotation factory, then find the corresponding machine id of the integration
        var sheetCuttingWorkingStep = request.PartType.Estimations
            .SelectMany(x => x.WorkingStepEstimations
                .Where(est => est.ResourceWorkingStepKey.WorkingStepKey is { PrimaryWorkingStep: WorkingStepTypeV1.SheetCutting, SecondaryWorkingStep: WorkingStepTypeV1.SheetCutting }))
            .FirstOrDefault();
        var resourceId = sheetCuttingWorkingStep?.ResourceWorkingStepKey.ResourceId.ToString();

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            // no machine found for bending, throw error without machines and log error
            throw new ApplicationException(
                $"No sheetCutting machine found in request of project-id: {request.ProjectId}, part-id: {request.PartType.Id}");
        }

        var cuttingMachineName = _machineMappingRepository.GetBySoftMachineId(resourceId);

        if (string.IsNullOrWhiteSpace(cuttingMachineName))
        {
            throw new ApplicationException(
                $"Machine could not be found in data/MachineMapping.xlsx. for machine-id: {resourceId}.");
        }

        return cuttingMachineName;
    }

    private double GetThickness(RequestManufacturabilityCheckOfPartTypeMessage request)
    {
        double thickness = 0;
        if (request.PartType.Material.SelectableArticles.SelectedArticle is { Dimensions.Thickness: not null })
        {
            // Thickness should be provided in the units of the machine.
            // Get the unit of measurement from the app.settings
            switch (_bySoftIntegrationSettings.MachineUnitOfMeasurement)
            {
                case MachineUnitOfMeasurementInch:
                    thickness = request.PartType.Material.SelectableArticles.SelectedArticle.Dimensions.Thickness?.Inches ?? 0;
                    break;
                case MachineUnitOfMeasurementMillimeters:
                    thickness = request.PartType.Material.SelectableArticles.SelectedArticle.Dimensions.Thickness?.Millimeters ?? 0;
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

        if (_bySoftIntegrationSettings.WarningsAsErrors.Length != 0)
        {
            if (checkPartResult.BendingDetails?.Any(x => _warningsAsErrors.Contains(x.Description)) ?? false)
            {
                isManufacturable = false;
            }

            if (checkPartResult.CuttingDetails?.Any(x => _warningsAsErrors.Contains(x.Description)) ?? false)
            {
                isManufacturable = false;
            }

            if (checkPartResult.GeometryDetails?.Any(x => _warningsAsErrors.Contains(x.Description)) ?? false)
            {
                isManufacturable = false;
            }
        }

        return new RequestManufacturabilityCheckOfPartTypeMessageResponse
        {
            ProjectId = request.ProjectId,
            PartTypeId = request.PartType.Id,
            IsManufacturable = isManufacturable,
            EventLogs = BuildEventLog(request, checkPartResult, _warningsAsErrors)
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

    private static List<EventLog> BuildEventLog(RequestManufacturabilityCheckOfPartTypeMessage request, CheckPartResponse checkPartResult,string[] warningsAsErrors)
    {
        var logs = new List<EventLog>();

        if (checkPartResult.BendingDetails != null && checkPartResult.BendingDetails.Any())
        {
            logs.AddRange(checkPartResult.BendingDetails.Select(bendingDetail => new EventLog
            {
                DateTime = DateTime.UtcNow,
                Message = $"{bendingDetail.Description}. Status: {bendingDetail.State}",
                Level =  warningsAsErrors.Contains(bendingDetail.Description, StringComparer.OrdinalIgnoreCase) ? EventLogLevel.Error : GetLogLevel(bendingDetail.State),
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
                Level = warningsAsErrors.Contains(cuttingDetail.Description, StringComparer.OrdinalIgnoreCase) ? EventLogLevel.Error : GetLogLevel(cuttingDetail.State),
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
                Level = warningsAsErrors.Contains(geometryDetail.Description, StringComparer.OrdinalIgnoreCase) ? EventLogLevel.Error : GetLogLevel(geometryDetail.State),
                ProjectId = request.ProjectId,
                PartTypeId = request.PartType.Id
            }));
        }

        return logs;
    }

    #endregion
}
