using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using Newtonsoft.Json;
using QF.BySoft.Entities;
using QF.BySoft.Manufacturability;
using QF.BySoft.Manufacturability.Helpers;
using QF.BySoft.Manufacturability.Models;
using QF.BySoft.Tests.Util;
using Xunit;

namespace QF.BySoft.Tests;

public class BySoftApiTests
{
    private const string StepFilePathName = "C:\\temp\\step.step";

    private const string SubDirectory = "manufacturability-check";

    // BySoft API variables for Mock
    private readonly string _apiBasePath;
    private readonly SetTechnologyResponse _bendingResponse;
    private readonly BySoftIntegrationSettings _bySoftIntegrationSettings;
    private readonly Mock<ILogger<BySoftApi>> _mockLogger;
    private readonly List<string> _partNameResult;

    public BySoftApiTests()
    {
        // The constructor runs for every test, so variables will be reset before each test.
        _mockLogger = new Mock<ILogger<BySoftApi>>();
        _bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();
        _apiBasePath =
            $"{_bySoftIntegrationSettings.BySoftApiServer}:{_bySoftIntegrationSettings.BySoftApiPort}/{_bySoftIntegrationSettings.BySoftApiRootPath}";
        _partNameResult = new List<string>
        {
            $"box://Parts/API/{SubDirectory}/step",
            "box://dummy"
        };
        _bendingResponse = new SetTechnologyResponse
        {
            Status = "Ok",
            Message = ""
        };
    }


    [Fact]
    public async Task ApiImportPartFailedShouldThrowErrorWithResponseBody()
    {
        // Arrange
        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(_bySoftIntegrationSettings);
        var httpMessageHandlerMock = HttpMessageHandlerMock(_apiBasePath, StepFilePathName, SubDirectory,
            _partNameResult, _bendingResponse, HttpStatusCode.BadRequest);
        var httpClient = httpMessageHandlerMock.CreateClient();

        var sut = new BySoftApi(
            httpClient,
            _mockLogger.Object,
            integrationSettingsMock.Object
        );

        // Act
        var act = () => sut.ImportPartAsync(StepFilePathName, SubDirectory);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>()
            .Where(e => e.Message.StartsWith("BySoft import part failed"));
    }

    [Fact]
    public async Task ApiGetUriFromPartNameFailedShouldThrowErrorAsync()
    {
        // Arrange
        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(_bySoftIntegrationSettings);
        var httpMessageHandlerMock =
            HttpMessageHandlerMock(_apiBasePath, StepFilePathName, SubDirectory, _partNameResult, _bendingResponse);
        // Overwrite the GetPartUrisName part handler, return false the first time, returns true the second call
        // http://localhost:56111/api/v1/Parts/GetPartUris?name=project-x
        var partName = Path.GetFileNameWithoutExtension(_partNameResult.First());
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Get,
                $"{_apiBasePath}/Parts/GetPartUris?name={partName.EscapeUriString()}")
            .ReturnsResponse(HttpStatusCode.BadRequest);

        var httpClient = httpMessageHandlerMock.CreateClient();

        var sut = new BySoftApi(
            httpClient,
            _mockLogger.Object,
            integrationSettingsMock.Object
        );

        // Act
        Func<Task> act = () => sut.GetUriFromPartNameAsync(partName, SubDirectory);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>()
            .Where(e => e.Message.StartsWith("BySoft getPartUris failed"));
    }

    [Fact]
    public async Task ApiUpdatePartFailedShouldThrowErrorAsync()
    {
        // Arrange
        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(_bySoftIntegrationSettings);
        var httpMessageHandlerMock =
            HttpMessageHandlerMock(_apiBasePath, StepFilePathName, SubDirectory, _partNameResult, _bendingResponse);
        // Overwrite the UpdatePart
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Post,
                $"{_apiBasePath}/Parts/Update?uri={_partNameResult.First().UrlEncode()}")
            .ReturnsResponse(HttpStatusCode.BadRequest);

        var httpClient = httpMessageHandlerMock.CreateClient();

        var sut = new BySoftApi(
            httpClient,
            _mockLogger.Object,
            integrationSettingsMock.Object
        );

        // Act
        var act = () => sut.UpdatePartAsync(_partNameResult.First(), "", "", "", 1, null);

        // Assert
        await act
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ApiDeletePartFailedShouldThrowErrorAsync()
    {
        // Arrange
        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(_bySoftIntegrationSettings);
        var httpMessageHandlerMock =
            HttpMessageHandlerMock(_apiBasePath, StepFilePathName, SubDirectory, _partNameResult, _bendingResponse);
        // Overwrite the Delete part
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Post,
                $"{_apiBasePath}/Parts/Delete?uri={_partNameResult.First().UrlEncode()}")
            .ReturnsResponse(HttpStatusCode.BadRequest);

        var httpClient = httpMessageHandlerMock.CreateClient();

        var sut = new BySoftApi(
            httpClient,
            _mockLogger.Object,
            integrationSettingsMock.Object
        );

        // Act
        var act = () => sut.DeletePartAsync(_partNameResult.First());

        // Assert
        await act
            .Should().ThrowAsync<HttpRequestException>();
    }


    private static Mock<IOptions<BySoftIntegrationSettings>> IntegrationSettingsMock(
        BySoftIntegrationSettings bySoftIntegrationSettings)
    {
        var integrationSettingsMock = new Mock<IOptions<BySoftIntegrationSettings>>();
        integrationSettingsMock
            .Setup(x => x.Value)
            .Returns(bySoftIntegrationSettings);
        return integrationSettingsMock;
    }

    private static Mock<HttpMessageHandler> HttpMessageHandlerMock(string apiBasePath, string stepFilePathName, string subDirectory,
        List<string> partNameResult,
        SetTechnologyResponse bendingResponse, HttpStatusCode importPartStatusCode = HttpStatusCode.OK,
        string importPartResponseContent = "")
    {
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        // First the general response for the requests that should return OK
        httpMessageHandlerMock
            .SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.OK);

        //http://localhost:12345/api/v1/Parts/ImportPart
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Post,
                $"{apiBasePath}/Parts/ImportPart?path={stepFilePathName.UrlEncode()}&subdirectory={subDirectory.UrlEncode()}")
            .ReturnsResponse(importPartStatusCode, importPartResponseContent);

        // Part name is the name of the step file without extension
        var partName = Path.GetFileNameWithoutExtension(stepFilePathName);
        var partNameResultJson = JsonConvert.SerializeObject(partNameResult);
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Get, $"{apiBasePath}/Parts/GetPartUris?name={partName.EscapeUriString()}")
            .ReturnsResponse(partNameResultJson, "application/json");

        // Update
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Post,
                $"{apiBasePath}/Parts/Update?uri={partNameResult.First().UrlEncode()}")
            .ReturnsResponse(HttpStatusCode.OK);

        // Bending tech
        var bendingResponseJson = JsonConvert.SerializeObject(bendingResponse);
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Post,
                $"{apiBasePath}/Parts/SetBendingTechnology?uri={partNameResult.First().UrlEncode()}&calculateDeduction=true")
            .ReturnsResponse(bendingResponseJson, "application/json");

        //http://localhost:12345/api/v1/Parts/Delete?=uri=xxx
        httpMessageHandlerMock
            .SetupRequest(HttpMethod.Post,
                $"{apiBasePath}/Parts/Delete?uri={partNameResult.First().UrlEncode()}")
            .ReturnsResponse(HttpStatusCode.OK);

        return httpMessageHandlerMock;
    }
}
