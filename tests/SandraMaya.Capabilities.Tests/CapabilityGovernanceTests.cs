using SandraMaya.Capabilities;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Configuration;
using SandraMaya.Capabilities.Models;
using SandraMaya.Capabilities.Persistence;
using SandraMaya.Capabilities.Services;

namespace SandraMaya.Capabilities.Tests;

public sealed class CapabilityGovernanceTests
{
    [Fact]
    public async Task RegisterAsync_StoresBuiltInCapabilityMetadata()
    {
        var clock = new StubTimeProvider(new DateTimeOffset(2025, 02, 01, 12, 00, 00, TimeSpan.Zero));
        var store = new InMemoryCapabilityStore();
        var registry = new CapabilityRegistryService(store, clock);

        var capability = await registry.RegisterAsync(CapabilityRegistrationRequest.CreateBuiltIn(
            id: "telegram.dispatch",
            name: "Telegram Dispatcher",
            version: "1.0.0",
            runtime: new CapabilityRuntimeDescriptor(CapabilityLanguage.CSharp, CapabilityRuntime.DotNet, "SandraMaya.Telegram.dll"),
            installPath: @"capabilities\builtins\telegram",
            permissions: new CapabilityPermissionSet([CapabilityPermission.FileSystemRead], ContainmentBoundary: "local-readonly"),
            description: "Built-in Telegram message dispatcher"));

        capability.Source.Kind.Should().Be(CapabilityKind.BuiltIn);
        capability.Status.Should().Be(CapabilityStatus.Enabled);
        capability.State.Should().Be(CapabilityState.Ready);
        Assert.Equal(clock.GetUtcNow(), capability.Timestamps.CreatedAt);
        capability.InstallPath.Should().Be(@"capabilities\builtins\telegram");
    }

    [Fact]
    public async Task EnableDisableRemoveAsync_TransitionsLifecycle()
    {
        var clock = new StubTimeProvider(new DateTimeOffset(2025, 02, 01, 12, 00, 00, TimeSpan.Zero));
        var store = new InMemoryCapabilityStore();
        var registry = new CapabilityRegistryService(store, clock);

        await registry.RegisterAsync(CapabilityRegistrationRequest.CreateGenerated(
            id: "job.crawler",
            name: "Job Crawler",
            version: "0.1.0",
            runtime: new CapabilityRuntimeDescriptor(CapabilityLanguage.TypeScript, CapabilityRuntime.NodeJs, @"dist\crawler.js"),
            installPath: @"capabilities\generated\job-crawler",
            permissions: new CapabilityPermissionSet([CapabilityPermission.NetworkAccess, CapabilityPermission.BrowserAutomation]),
            reference: "conversation://turn-42",
            createdBy: "assistant"));

        clock.Advance(TimeSpan.FromMinutes(1));
        var enabled = await registry.EnableAsync("job.crawler");

        clock.Advance(TimeSpan.FromMinutes(1));
        var disabled = await registry.DisableAsync("job.crawler");

        clock.Advance(TimeSpan.FromMinutes(1));
        var removed = await registry.RemoveAsync("job.crawler");

        enabled.Status.Should().Be(CapabilityStatus.Enabled);
        Assert.Equal(clock.GetUtcNow() - TimeSpan.FromMinutes(2), enabled.Timestamps.EnabledAt);
        disabled.Status.Should().Be(CapabilityStatus.Disabled);
        Assert.Equal(clock.GetUtcNow() - TimeSpan.FromMinutes(1), disabled.Timestamps.DisabledAt);
        removed.Status.Should().Be(CapabilityStatus.Removed);
        removed.State.Should().Be(CapabilityState.Removed);
        Assert.Equal(clock.GetUtcNow(), removed.Timestamps.RemovedAt);
    }

    [Fact]
    public async Task RecordExecutionAsync_UpdatesHealthAndHistory()
    {
        var clock = new StubTimeProvider(new DateTimeOffset(2025, 02, 01, 12, 00, 00, TimeSpan.Zero));
        var store = new InMemoryCapabilityStore();
        var registry = new CapabilityRegistryService(store, clock);
        var activity = new CapabilityActivityService(store);

        await registry.RegisterAsync(CapabilityRegistrationRequest.CreateBuiltIn(
            id: "jobs.summary",
            name: "Jobs Summary",
            version: "1.0.0",
            runtime: new CapabilityRuntimeDescriptor(CapabilityLanguage.Python, CapabilityRuntime.Python, "summary.py"),
            installPath: @"capabilities\builtins\jobs-summary",
            permissions: new CapabilityPermissionSet([CapabilityPermission.FileSystemRead, CapabilityPermission.NetworkAccess])));

        var record = new CapabilityExecutionRecord(
            ExecutionId: "exec-1",
            CapabilityId: "jobs.summary",
            RequestedAt: clock.GetUtcNow(),
            Status: CapabilityExecutionStatus.Succeeded,
            RequestedBy: "scheduler",
            Command: "python",
            WorkingDirectory: @"capabilities\builtins\jobs-summary",
            Arguments: ["summary.py"],
            StartedAt: clock.GetUtcNow(),
            CompletedAt: clock.GetUtcNow().AddSeconds(10),
            ContainmentProfile: "networked");

        await activity.RecordExecutionAsync(record);

        var capability = await registry.GetAsync("jobs.summary");
        var history = await activity.GetExecutionHistoryAsync("jobs.summary");

        capability.Should().NotBeNull();
        capability!.Health.Status.Should().Be(CapabilityHealthStatus.Healthy);
        capability.State.Should().Be(CapabilityState.Ready);
        Assert.Single(history);
        history[0].ExecutionId.Should().Be("exec-1");
    }

    [Fact]
    public async Task ResolveAsync_BuildsNodeExecutionPlan()
    {
        var clock = new StubTimeProvider(new DateTimeOffset(2025, 02, 01, 12, 00, 00, TimeSpan.Zero));
        var store = new InMemoryCapabilityStore();
        var registry = new CapabilityRegistryService(store, clock);
        var resolver = new CapabilityExecutionPlanResolver(store, clock);

        await registry.RegisterAsync(CapabilityRegistrationRequest.CreateGenerated(
            id: "crawler.playwright",
            name: "Crawler Playwright",
            version: "0.2.0",
            runtime: new CapabilityRuntimeDescriptor(
                CapabilityLanguage.TypeScript,
                CapabilityRuntime.Playwright,
                @"dist\index.js",
                RuntimeVersion: "20",
                Arguments: ["--site", "jobs-ch"]),
            installPath: @"capabilities\generated\crawler-playwright",
            permissions: new CapabilityPermissionSet([CapabilityPermission.NetworkAccess, CapabilityPermission.BrowserAutomation]),
            reference: "conversation://turn-85",
            createdBy: "assistant",
            initialStatus: CapabilityStatus.Enabled,
            initialState: CapabilityState.Ready));

        var plan = await resolver.ResolveAsync("crawler.playwright");

        plan.Command.Should().Be("node");
        plan.WorkingDirectory.Should().Be(@"capabilities\generated\crawler-playwright");
        plan.Arguments[0].Should().Be(@"capabilities\generated\crawler-playwright\dist\index.js");
        plan.ContainmentProfile.Should().Be("elevated");
        plan.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_UsesConfiguredRuntimeCommands()
    {
        var store = new InMemoryCapabilityStore();
        var registry = new CapabilityRegistryService(store);
        var resolver = new CapabilityExecutionPlanResolver(
            store,
            commandOptions: new CapabilityRuntimeCommandOptions
            {
                PlaywrightCommand = "playwright-wrapper",
                PythonCommand = "python3"
            });

        await registry.RegisterAsync(CapabilityRegistrationRequest.CreateGenerated(
            id: "crawler.playwright",
            name: "Crawler Playwright",
            version: "0.2.0",
            runtime: new CapabilityRuntimeDescriptor(
                CapabilityLanguage.TypeScript,
                CapabilityRuntime.Playwright,
                @"dist\index.js"),
            installPath: @"capabilities\generated\crawler-playwright",
            permissions: new CapabilityPermissionSet([CapabilityPermission.NetworkAccess]),
            reference: "conversation://turn-86",
            createdBy: "assistant",
            initialStatus: CapabilityStatus.Enabled,
            initialState: CapabilityState.Ready));

        await registry.RegisterAsync(CapabilityRegistrationRequest.CreateBuiltIn(
            id: "jobs.summary",
            name: "Jobs Summary",
            version: "1.0.0",
            runtime: new CapabilityRuntimeDescriptor(CapabilityLanguage.Python, CapabilityRuntime.Python, "summary.py"),
            installPath: @"capabilities\builtins\jobs-summary",
            permissions: new CapabilityPermissionSet([CapabilityPermission.FileSystemRead])));

        var playwrightPlan = await resolver.ResolveAsync("crawler.playwright");
        var pythonPlan = await resolver.ResolveAsync("jobs.summary");

        playwrightPlan.Command.Should().Be("playwright-wrapper");
        pythonPlan.Command.Should().Be("python3");
    }

    [Fact]
    public async Task FileCapabilityStore_PersistsCapabilityAuditData()
    {
        var rootPath = Path.Combine(AppContext.BaseDirectory, "FileStoreArtifacts", Guid.NewGuid().ToString("N"));

        try
        {
            var store = new FileCapabilityStore(new CapabilityStoreOptions(rootPath));
            var capability = CapabilityRegistrationRequest.CreateBuiltIn(
                id: "reporting.monthly",
                name: "Monthly Reporting",
                version: "1.0.0",
                runtime: new CapabilityRuntimeDescriptor(CapabilityLanguage.CSharp, CapabilityRuntime.DotNet, "SandraMaya.Reporting.dll"),
                installPath: @"capabilities\builtins\reporting",
                permissions: new CapabilityPermissionSet([CapabilityPermission.FileSystemRead, CapabilityPermission.DatabaseAccess]));
            var registry = new CapabilityRegistryService(store, new StubTimeProvider(new DateTimeOffset(2025, 02, 01, 12, 00, 00, TimeSpan.Zero)));
            var activity = new CapabilityActivityService(store);

            await registry.RegisterAsync(capability);
            await activity.RecordInstallationAsync(new CapabilityInstallProvenanceRecord(
                InstallId: "install-1",
                CapabilityId: "reporting.monthly",
                InstalledAt: new DateTimeOffset(2025, 02, 01, 12, 30, 00, TimeSpan.Zero),
                InstalledBy: "bootstrap",
                Source: capability.Source,
                InstallPath: capability.InstallPath,
                PermissionSnapshot: capability.Permissions.Scopes,
                ValidationSteps: ["manifest-validated", "smoke-tested"]));
            await activity.RecordExecutionAsync(new CapabilityExecutionRecord(
                ExecutionId: "execution-1",
                CapabilityId: "reporting.monthly",
                RequestedAt: new DateTimeOffset(2025, 02, 02, 08, 00, 00, TimeSpan.Zero),
                Status: CapabilityExecutionStatus.Succeeded,
                RequestedBy: "scheduler",
                Command: "dotnet",
                WorkingDirectory: capability.InstallPath,
                Arguments: ["SandraMaya.Reporting.dll"]));

            var reloadedStore = new FileCapabilityStore(new CapabilityStoreOptions(rootPath));
            var capabilities = await reloadedStore.ListCapabilitiesAsync();
            var installs = await reloadedStore.ListInstallRecordsAsync("reporting.monthly");
            var executions = await reloadedStore.ListExecutionRecordsAsync("reporting.monthly");

            Assert.Single(capabilities);
            Assert.Single(installs);
            Assert.Single(executions);
            capabilities[0].Health.Status.Should().Be(CapabilityHealthStatus.Healthy);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private sealed class InMemoryCapabilityStore : ICapabilityStore
    {
        private readonly Dictionary<string, CapabilityRecord> _capabilities = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CapabilityInstallProvenanceRecord> _installs = [];
        private readonly List<CapabilityExecutionRecord> _executions = [];

        public Task<CapabilityRecord?> GetCapabilityAsync(string capabilityId, CancellationToken cancellationToken = default)
        {
            _capabilities.TryGetValue(capabilityId, out var capability);
            return Task.FromResult(capability);
        }

        public Task<IReadOnlyList<CapabilityRecord>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CapabilityRecord>>(_capabilities.Values.ToArray());

        public Task UpsertCapabilityAsync(CapabilityRecord capability, CancellationToken cancellationToken = default)
        {
            _capabilities[capability.Id] = capability;
            return Task.CompletedTask;
        }

        public Task AddInstallRecordAsync(CapabilityInstallProvenanceRecord record, CancellationToken cancellationToken = default)
        {
            _installs.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CapabilityInstallProvenanceRecord>> ListInstallRecordsAsync(string capabilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CapabilityInstallProvenanceRecord>>(
                _installs.Where(x => string.Equals(x.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase)).ToArray());

        public Task AddExecutionRecordAsync(CapabilityExecutionRecord record, CancellationToken cancellationToken = default)
        {
            _executions.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CapabilityExecutionRecord>> ListExecutionRecordsAsync(string capabilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CapabilityExecutionRecord>>(
                _executions.Where(x => string.Equals(x.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
    }
}

