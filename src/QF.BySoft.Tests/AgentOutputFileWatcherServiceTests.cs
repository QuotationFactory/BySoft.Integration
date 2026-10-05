using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QF.BySoft.Entities;
using QF.BySoft.Integration.Features.AgentOutputFile;
using QF.BySoft.Integration.Features.BySoftIntegration;
using QF.BySoft.Tests.Util;
using Xunit;

namespace QF.BySoft.Tests;

public class AgentOutputFileWatcherServiceTests
{
    [Fact]
    public async Task OnAllChangesShouldResolveNewBySoftIntegrationPerCreatedFile()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"{nameof(AgentOutputFileWatcherServiceTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);

        var state = new TestState(expectedHandledCount: 2);
        var settings = SettingsBuilder.GetBySoftIntegrationSettings();
        settings.RootDirectory = rootDirectory;

        var serviceProvider = BuildServiceProvider(settings, state);
        var sut = new TestAgentOutputFileWatcherService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IOptions<BySoftIntegrationSettings>>(),
            serviceProvider.GetRequiredService<ILogger<AgentOutputFileWatcherService>>()
        );

        try
        {
            sut.RaiseAllChanges(new FileSystemEventArgs(WatcherChangeTypes.Created, rootDirectory, "first.json"));
            sut.RaiseAllChanges(new FileSystemEventArgs(WatcherChangeTypes.Created, rootDirectory, "second.json"));

            await state.AllHandled.Task.WaitAsync(TimeSpan.FromSeconds(10));

            state.HandledCount.Should().Be(2);
            state.ResolutionCount.Should().Be(2, "a fresh scoped handler dependency graph should be resolved for each event");
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
            await serviceProvider.DisposeAsync();
            Directory.Delete(rootDirectory, true);
        }
    }

    private static ServiceProvider BuildServiceProvider(BySoftIntegrationSettings settings, TestState state)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<BySoftIntegrationSettings>>(Options.Create(settings));
        services.AddSingleton(Mock.Of<ILogger<AgentOutputFileCreatedHandler>>());
        services.AddSingleton(Mock.Of<ILogger<AgentOutputFileWatcherService>>());
        services.AddTransient<IBySoftIntegration>(_ =>
        {
            Interlocked.Increment(ref state.ResolutionCount);
            var bySoftIntegrationMock = new Mock<IBySoftIntegration>();
            bySoftIntegrationMock
                .Setup(x => x.HandleManufacturabilityCheckRequestAsync(It.IsAny<string>()))
                .Returns(() =>
                {
                    if (Interlocked.Increment(ref state.HandledCount) == state.ExpectedHandledCount)
                    {
                        state.AllHandled.TrySetResult(true);
                    }

                    return Task.CompletedTask;
                });
            return bySoftIntegrationMock.Object;
        });
        services.AddTransient<AgentOutputFileCreatedHandler>();

        return services.BuildServiceProvider();
    }

    private sealed class TestAgentOutputFileWatcherService : AgentOutputFileWatcherService
    {
        public TestAgentOutputFileWatcherService(
            IServiceScopeFactory serviceScopeFactory,
            IOptions<BySoftIntegrationSettings> options,
            ILogger<AgentOutputFileWatcherService> logger)
            : base(serviceScopeFactory, options, logger)
        {
        }

        public void RaiseAllChanges(FileSystemEventArgs fileSystemEventArgs)
        {
            OnAllChanges(this, fileSystemEventArgs);
        }
    }

    private sealed class TestState(int expectedHandledCount)
    {
        public TaskCompletionSource<bool> AllHandled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ExpectedHandledCount { get; } = expectedHandledCount;
        public int HandledCount;
        public int ResolutionCount;
    }
}


