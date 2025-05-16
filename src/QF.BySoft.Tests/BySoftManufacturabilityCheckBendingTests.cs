using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MetalHeaven.Agent.Shared.External.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QF.BySoft.Entities;
using QF.BySoft.Entities.Repositories;
using QF.BySoft.Manufacturability;
using QF.BySoft.Manufacturability.Interfaces;
using QF.BySoft.Manufacturability.Models;
using QF.BySoft.Tests.Util;
using Xunit;

namespace QF.BySoft.Tests;

public class BySoftManufacturabilityCheckBendingTests
{
    // BySoft API variables for Mock
    private const string StepFilePathName = "C:\\temp\\step.step";
    private const string SubDirectory = "manufacturability-check";
    private readonly Mock<ILogger<BySoftManufacturabilityCheckBending>> _mockLogger;
    private readonly Guid _partIdValue;

    private readonly string _partNameResult;

    // Variables to setup the request
    private readonly Guid _projectIdValue;
    private SetTechnologyResponse _bendingResponse;

    private string _machineIntegration;

    // Result from material/machine repository for Mock
    private string _materialIntegration;
    private double _thickNess;

    public BySoftManufacturabilityCheckBendingTests()
    {
        // The constructor runs for every test, so variables will be reset before each test.
        _mockLogger = new Mock<ILogger<BySoftManufacturabilityCheckBending>>();
        _projectIdValue = Guid.NewGuid();
        _partIdValue = Guid.NewGuid();
        _thickNess = 0.3;
        _materialIntegration = "material-int-a";
        _machineIntegration = "machine-int-b";
        _partNameResult = $"box://Parts/API/{SubDirectory}/step";
        _bendingResponse = new SetTechnologyResponse
        {
            Status = "Ok",
            Message = ""
        };
    }


    [Fact]
    public async Task ManufacturabilityCheckShouldCreateResponse()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        var bySoftApiMock = BySoftApiMock(StepFilePathName, SubDirectory, _partNameResult, _bendingResponse);

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        var result = await sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        result.ProjectId.Should().Be(_projectIdValue);
        result.PartTypeId.Should().Be(_partIdValue);
        result.IsManufacturable.Should().BeTrue();
        result.EventLogs.Should().NotBeNull();
        result.EventLogs.Should().BeEmpty();
    }


    [Fact]
    public async Task InvalidMachineMeasurementOfUnitShouldThrowError()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();
        bySoftIntegrationSettings.MachineUnitOfMeasurement = "wrong";

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        var bySoftApiMock = new Mock<IBySoftApi>();

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        Func<Task> act = () => sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>()
            .WithMessage("Machine units of measurement not provided correctly in the app.settings. Possible values are: mm or inch");
    }

    [Theory]
    [InlineData(null, "123", "api/v1")]
    [InlineData("", "123", "api/v1")]
    [InlineData("  ", "123", "api/v1")]
    [InlineData("http://localhost", null, "api/v1")]
    [InlineData("http://localhost", "", "api/v1")]
    [InlineData("http://localhost", "  ", "api/v1")]
    [InlineData("http://localhost", "123", null)]
    [InlineData("http://localhost", "123", "")]
    [InlineData("http://localhost", "123", "  ")]
    public async Task EmptyApiSettingsShouldThrowError(string apiServer, string apiPort, string apiRoot)
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();
        bySoftIntegrationSettings.BySoftApiServer = apiServer;
        bySoftIntegrationSettings.BySoftApiPort = apiPort;
        bySoftIntegrationSettings.BySoftApiRootPath = apiRoot;

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        var bySoftApiMock = new Mock<IBySoftApi>();

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        Func<Task> act = () => sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public async Task RequestWithoutThicknessShouldThrowError()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();

        // Set thickness to zero
        _thickNess = 0;


        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        var bySoftApiMock = new Mock<IBySoftApi>();

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        Func<Task> act = () => sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>()
            .WithMessage("Thickness of material not found in the request.");
    }

    [Fact]
    public async Task MaterialIntegrationNotFoundShouldThrowError()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();

        // Result from material/machine repository for Mock
        _materialIntegration = "";

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        var bySoftApiMock = new Mock<IBySoftApi>();

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        Func<Task> act = () => sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>()
            .Where(e => e.Message.StartsWith("Material mapping not found in data/MaterialMapping.xlsx for material-id"));
    }

    [Fact]
    public async Task MachineIntegrationNotFoundShouldThrowError()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();

        // Result from material/machine repository for Mock
        _machineIntegration = "";

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        var bySoftApiMock = new Mock<IBySoftApi>();

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        Func<Task> act = () => sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        await act
            .Should().ThrowAsync<ApplicationException>()
            .Where(e => e.Message.StartsWith("Machine could not be found in data/MachineMapping.xlsx. for machine-id"));
    }


    [Fact]
    public async Task BendingResultNotSuccessfulShouldCreateResponseWithEventLog()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();

        // BySoft API variables for Mock
        _bendingResponse = new SetTechnologyResponse
        {
            Status = "Warning",
            Message = "Could not create bending"
        };

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);
        // Overwrite the BendingTech
        var bySoftApiMock = BySoftApiMock(StepFilePathName, SubDirectory, _partNameResult, _bendingResponse);

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        var result = await sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        result.EventLogs.Should().HaveCount(1);
        result.EventLogs.First().Message.Should().Be($"{_bendingResponse.Message}. Status: {_bendingResponse.Status}");
    }

    [Fact(Skip = "TODO create part exists test")]
    public Task IfPartExistsDeleteShouldBeCalled()
    {
        throw new NotImplementedException();
    }


    [Fact]
    public async Task IfSavePartIsTrueApiDeletePartShouldNotBeCalled()
    {
        // Arrange
        var bySoftIntegrationSettings = SettingsBuilder.GetBySoftIntegrationSettings();
        bySoftIntegrationSettings.SavePartInBySoft = true;

        // Mocks
        var integrationSettingsMock = IntegrationSettingsMock(bySoftIntegrationSettings);
        var machineRepositoryMock = MachineRepositoryMock(_machineIntegration);
        var materialRepositoryMock = MaterialRepositoryMock(_materialIntegration);

        var bySoftApiMock = BySoftApiMock(StepFilePathName, SubDirectory, _partNameResult, _bendingResponse);

        var sut = new BySoftManufacturabilityCheckBending(
            integrationSettingsMock.Object,
            machineRepositoryMock.Object,
            materialRepositoryMock.Object,
            _mockLogger.Object,
            bySoftApiMock.Object
        );
        var request = RequestManufacturabilityCheckOfPartTypeMessage(_projectIdValue, _partIdValue, _thickNess);

        // Act
        var result = await sut.ManufacturabilityCheckBendingAsync(request, StepFilePathName);

        // Assert
        bySoftApiMock.Invocations.Should().NotContain(a => a.Method.Name == nameof(IBySoftApi.DeletePartAsync));
        result.ProjectId.Should().Be(_projectIdValue);
        result.PartTypeId.Should().Be(_partIdValue);
        result.IsManufacturable.Should().BeTrue();
    }

    private static RequestManufacturabilityCheckOfPartTypeMessage RequestManufacturabilityCheckOfPartTypeMessage(Guid projectIdValue,
        Guid partIdValue, double thickNess)
    {
        var stepFileUrlValue = new Uri("https://hello-url");
        var machineId = Guid.NewGuid();
        var materialId = Guid.NewGuid().ToString();
        var tokensValue = new List<string>
        {
            "hello",
            "world",
            "how",
            "are",
            "you"
        };
        var request = RequestBuilder.GetRequest(projectIdValue, partIdValue, stepFileUrlValue, machineId, materialId, tokensValue,
            thickNess);
        return request;
    }

    private static Mock<IMachineMappingRepository> MachineRepositoryMock(string machineIntegration)
    {
        var machineRepositoryMock = new Mock<IMachineMappingRepository>();
        machineRepositoryMock
            .Setup(x => x.GetBySoftMachineId(It.IsAny<string>()))
            .Returns(machineIntegration);
        return machineRepositoryMock;
    }

    private static Mock<IMaterialMappingRepository> MaterialRepositoryMock(string materialIntegration)
    {
        var materialRepositoryMock = new Mock<IMaterialMappingRepository>();
        materialRepositoryMock
            .Setup(x => x.GetMaterialCodeFromKeywords(It.IsAny<IEnumerable<string>>()))
            .Returns(materialIntegration);
        materialRepositoryMock
            .Setup(x => x.GetMaterialCodeFromArticle(It.IsAny<string>()))
            .Returns(materialIntegration);
        return materialRepositoryMock;
    }

    private static Mock<IOptions<BySoftIntegrationSettings>> IntegrationSettingsMock(BySoftIntegrationSettings bySoftIntegrationSettings)
    {
        var integrationSettingsMock = new Mock<IOptions<BySoftIntegrationSettings>>();
        integrationSettingsMock
            .Setup(x => x.Value)
            .Returns(bySoftIntegrationSettings);
        return integrationSettingsMock;
    }

    private static Mock<IBySoftApi> BySoftApiMock(string stepFilePathName, string subDirectory, string partNameResult,
        SetTechnologyResponse bendingResponse)
    {
        var bySoftApiMock = new Mock<IBySoftApi>();

        // Part name is the name of the step file without extension
        var partName = Path.GetFileNameWithoutExtension(stepFilePathName);

        // Mock get uri in sequence, because the first time it's called, is to check if it exists.
        // return null to indicate that it does not exists.
        bySoftApiMock
            .SetupSequence(x => x.GetUriFromPartNameAsync(partName, subDirectory))
            .ReturnsAsync((string)null)
            .ReturnsAsync(partNameResult);

        // Bending tech
        bySoftApiMock.Setup(x => x.SetBendingTechnologyAsync(partNameResult))
            .ReturnsAsync(bendingResponse);

        return bySoftApiMock;
    }
}
