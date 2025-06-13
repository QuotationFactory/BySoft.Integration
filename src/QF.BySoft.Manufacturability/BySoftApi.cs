using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QF.BySoft.Entities;
using QF.BySoft.Manufacturability.Helpers;
using QF.BySoft.Manufacturability.Interfaces;
using QF.BySoft.Manufacturability.Models;

namespace QF.BySoft.Manufacturability;

public class BySoftApi : IBySoftApi
{
    private readonly BySoftIntegrationSettings _bySoftIntegrationSettings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BySoftApi> _logger;

    public BySoftApi(
        HttpClient httpClient,
        ILogger<BySoftApi> logger,
        IOptions<BySoftIntegrationSettings> bySoftIntegrationSettings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _bySoftIntegrationSettings = bySoftIntegrationSettings.Value;
    }

    public async Task ImportPartAsync(string path, string subDirectory, bool secondTry = false)
    {
        //Example: http://localhost:56111/api/v1/Parts/ImportPart?path=c%3A%5Ctemp%5Cproject-x.step&subdirectory=mh-test
        var requestUri = $"{GetApiBasePath()}/Parts/ImportPart?path={path.UrlEncode()}&subdirectory={subDirectory.UrlEncode()}";
        _logger.LogDebug("ImportPart. Uri: {RequestUri}", requestUri);

        var response = await _httpClient.PostAsync(requestUri, null);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            throw new ApplicationException($"BySoft import part failed: {responseContent}");
        }
    }

    public async Task<string> GetUriFromPartNameAsync(string partName, string subDirectory)
    {
        //Example: http://localhost:56111/api/v1/Parts/GetPartUris?name=project-x
        // Response = array with strings (uri's)
        // [
        // "box://Parts/API/mh-test/project-x#98a5641f-efa2-44b1-a51f-11dbbdb440d8"
        //    ]

        var requestUri = $"{GetApiBasePath()}/Parts/GetPartUris?name={partName.EscapeUriString()}";
        _logger.LogDebug("GetPartUris. Uri: {RequestUri}", requestUri);

        var response = await _httpClient.GetAsync(requestUri);

        // Throws an error in not successful
        if (!response.IsSuccessStatusCode)
        {
            var responseContentFailed = await response.Content.ReadAsStringAsync();
            throw new ApplicationException($"BySoft getPartUris failed: {responseContentFailed}");
        }

        var responseContent = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();
        // There could be more than one part with the same name. But the sub directory is part
        // of the URI, so we filter on the sub directory.
        var uri = responseContent.FirstOrDefault(r => r.Contains(subDirectory));
        // if (uri == null)
        // {
        //     throw new ApplicationException($"Part name could not be retrieved. Part name: {partName}");
        // }

        return uri;
    }

    public async Task UpdatePartAsync(string partUri, string materialName, string bendingMachineName, string cuttingMachineName,
        double thickness, int? rotationAllowance)
    {
        // Example call
        // http://localhost:56111/api/v1/Parts/Update?uri=box%3A%2F%2FParts%2FAPI%2Fmh-test%2Fproject-x%2398a5641f-efa2-44b1-a51f-11dbbdb440d8
        // {
        //     "materialName": "RVS304",
        //     "bendingMachineName": "Xpert-Pro-150x3100-EH200-XPT-T12-HA5-Lams-AK",
        //     "thickness": 1,
        //     "cuttingMachineName": "BySoft-API-Server",
        //     "priority": "1",
        //     "rotationAllowance": 1/5/10/30/45/90/180
        // }


        // We need to put the parameters in the URL, because we can't combine json content and query parameters in the content
        var url = $"{GetApiBasePath()}/Parts/Update?uri={partUri.UrlEncode()}";
        var content = new UpdatePartInfo
        {
            MaterialName = materialName,
            CuttingMachineName = cuttingMachineName,
            BendingMachineName = bendingMachineName,
            Thickness = thickness,
            Priority = "1",
            RotationAllowance = rotationAllowance
        };

        _logger.LogDebug("UpdatePartAsync. with materialName: '{materialName}', bendingMachineName:'{bendingMachineName}', cuttingMachineName: '{cuttingMachineName}' Url: {Url}", materialName, bendingMachineName, cuttingMachineName, url);
        var response = await _httpClient.PostAsJsonAsync(url, content);
        // Throws an error in not successful
        response.EnsureSuccessStatusCode();
    }

    public async Task<SetTechnologyResponse> SetBendingTechnologyAsync(string partUri)
    {
        // Example call
        // http://localhost:56111/api/v1/Parts/SetBendingTechnology?uri=box%3A%2F%2FParts%2FAPI%2Fmh-test%2FBeugel%25204%23eab4070f-7329-43b4-9a2a-f74219beb60f&calculateDeduction=true&calculateBendingTime=false
        // Example responses:
        // {
        //     "status": "Warning",
        //     "message": "Unable to create the bending sequence."
        // }
        //
        // {
        //     "status": "Ok",
        //     "message": ""
        // }

        var requestUri =
            $"{GetApiBasePath()}/Parts/SetBendingTechnology?uri={partUri.UrlEncode()}&calculateDeduction=true&calculateBendingTime=false";
        _logger.LogDebug("SetBendingTechnology. Uri: {RequestUri}", requestUri);

        var response = await _httpClient.PostAsync(requestUri, null);
        // Throws an error in not successful
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadFromJsonAsync<SetTechnologyResponse>();
        var responseForLog = JsonSerializer.Serialize(responseContent);
        _logger.LogDebug("SetBendingTechnology response: {Log}", responseForLog);

        return responseContent;
    }

    public async Task SetCuttingTechnologyAsync(string partUri)
    {
        var requestUri = $"{GetApiBasePath()}/Parts/SetCuttingTechnology?uri={partUri.UrlEncode()}";
        _logger.LogDebug("SetCuttingTechnology. Uri: {RequestUri}", requestUri);

        var content = new CuttingTechnologyInfo
        {
            CalculateCuttingTime = true,
            NoCuttingTechnologyOnOuterContour = false
        };

        var response = await _httpClient.PostAsJsonAsync(requestUri, content);

        // Throws an error in not successful
        response.EnsureSuccessStatusCode();
        // We do not check the response. We only set it.
    }

    public async Task DeletePartAsync(string partUri)
    {
        // Example call:
        // http://localhost:56111/api/v1/Parts/Delete?uri=box%3A%2F%2FParts%2FAPI%2Fmh-test%2FBeugel%25204%23eab4070f-7329-43b4-9a2a-f74219beb60f

        var requestUri = $"{GetApiBasePath()}/Parts/Delete?uri={partUri.UrlEncode()}";
        _logger.LogDebug("Parts/Delete. Uri: {RequestUri}", requestUri);

        var response = await _httpClient.PostAsync(requestUri, null);
        // Throws an error in not successful
        response.EnsureSuccessStatusCode();
    }

    public async Task<CheckPartResponse> CheckPartAsync(string partUri)
    {
        var requestUri = $"{GetApiBasePath()}/Parts/CheckPart?uri={partUri.UrlEncode()}";
        _logger.LogDebug("Parts/CheckPart. Uri: {RequestUri}", requestUri);

        var response = await _httpClient.GetAsync(requestUri);
        // Throws an error if not successful
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadFromJsonAsync<CheckPartResponse>();
        var responseForLog = JsonSerializer.Serialize(responseContent);
        _logger.LogDebug("CheckPart response: {Log}", responseForLog);

        return responseContent;
    }

    /// <summary>
    ///     Get the base path of the BySoft API
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    ///     Example URI from the manual:
    ///     http://localhost:56111/api/v1/Parts/Info?uri=box://Parts/PMC/1234#19f4dcd3-cf9e-4698-bc3f-9e34ec603910
    ///     This function creates the part (following the example): http://localhost:56111/api/v1
    /// </remarks>
    private string GetApiBasePath()
    {
        return $"{_bySoftIntegrationSettings.BySoftApiServer}:{_bySoftIntegrationSettings.BySoftApiPort}/{_bySoftIntegrationSettings.BySoftApiRootPath}";
    }
}
