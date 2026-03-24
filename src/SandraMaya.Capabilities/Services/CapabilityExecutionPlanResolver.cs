using SandraMaya.Capabilities.Configuration;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Services;

public sealed class CapabilityExecutionPlanResolver(
    ICapabilityStore store,
    TimeProvider? timeProvider = null,
    CapabilityRuntimeCommandOptions? commandOptions = null) : ICapabilityExecutionPlanResolver
{
    private readonly ICapabilityStore _store = store;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly CapabilityRuntimeCommandOptions _commandOptions = commandOptions ?? new CapabilityRuntimeCommandOptions();

    public async Task<CapabilityExecutionPlan> ResolveAsync(string capabilityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException("Capability id is required.", nameof(capabilityId));
        }

        var capability = await _store.GetCapabilityAsync(capabilityId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new CapabilityGovernanceException($"Capability '{capabilityId}' was not found.");

        if (capability.Status != CapabilityStatus.Enabled)
        {
            throw new CapabilityGovernanceException($"Capability '{capability.Id}' is not enabled.");
        }

        if (capability.State is CapabilityState.PendingInstall or CapabilityState.Removed)
        {
            throw new CapabilityGovernanceException($"Capability '{capability.Id}' is not install-ready.");
        }

        var entryPoint = ResolveEntryPoint(capability.InstallPath, capability.Runtime.EntryPoint);
        var (command, arguments) = ResolveCommand(capability.Runtime, entryPoint);

        return new CapabilityExecutionPlan(
            CapabilityId: capability.Id,
            Name: capability.Name,
            Version: capability.Version,
            Kind: capability.Source.Kind,
            Command: command,
            WorkingDirectory: ResolveWorkingDirectory(capability),
            ContainmentProfile: ResolveContainment(capability.Permissions),
            DeclaredPermissions: capability.Permissions.Scopes,
            RequiresApproval: RequiresApproval(capability),
            ResolvedAt: _timeProvider.GetUtcNow(),
            Arguments: arguments);
    }

    private static string ResolveWorkingDirectory(CapabilityRecord capability) =>
        string.IsNullOrWhiteSpace(capability.Runtime.WorkingDirectory)
            ? capability.InstallPath
            : (Path.IsPathRooted(capability.Runtime.WorkingDirectory)
                ? capability.Runtime.WorkingDirectory
                : Path.Combine(capability.InstallPath, capability.Runtime.WorkingDirectory));

    private (string Command, string[] Arguments) ResolveCommand(CapabilityRuntimeDescriptor runtime, string entryPoint)
    {
        var arguments = new List<string>();
        string command;

        switch (runtime.Runtime)
        {
            case CapabilityRuntime.DotNet:
                command = _commandOptions.DotNetCommand;
                arguments.Add(entryPoint);
                break;
            case CapabilityRuntime.NodeJs:
                command = _commandOptions.NodeCommand;
                arguments.Add(entryPoint);
                break;
            case CapabilityRuntime.Playwright:
                command = _commandOptions.PlaywrightCommand;
                arguments.Add(entryPoint);
                break;
            case CapabilityRuntime.Python:
                command = _commandOptions.PythonCommand;
                arguments.Add(entryPoint);
                break;
            case CapabilityRuntime.PowerShell:
                command = _commandOptions.PowerShellCommand;
                arguments.Add("-File");
                arguments.Add(entryPoint);
                break;
            case CapabilityRuntime.Bash:
                command = _commandOptions.BashCommand;
                arguments.Add(entryPoint);
                break;
            case CapabilityRuntime.Executable:
                command = entryPoint;
                break;
            default:
                throw new CapabilityGovernanceException($"Capability runtime '{runtime.Runtime}' is not supported.");
        }

        arguments.AddRange(runtime.Arguments);
        return (command, arguments.ToArray());
    }

    private static string ResolveEntryPoint(string installPath, string entryPoint) =>
        Path.IsPathRooted(entryPoint) ? entryPoint : Path.Combine(installPath, entryPoint);

    private static string ResolveContainment(CapabilityPermissionSet permissions)
    {
        if (!string.IsNullOrWhiteSpace(permissions.ContainmentBoundary))
        {
            return permissions.ContainmentBoundary;
        }

        var scopes = permissions.Scopes;
        if (scopes.Contains(CapabilityPermission.SecretsAccess) ||
            scopes.Contains(CapabilityPermission.ProcessExecution) ||
            scopes.Contains(CapabilityPermission.FileSystemWrite) ||
            scopes.Contains(CapabilityPermission.BrowserAutomation))
        {
            return "elevated";
        }

        if (scopes.Contains(CapabilityPermission.NetworkAccess) ||
            scopes.Contains(CapabilityPermission.ExternalApiAccess))
        {
            return "networked";
        }

        return "local-readonly";
    }

    private static bool RequiresApproval(CapabilityRecord capability) =>
        capability.Source.Kind == CapabilityKind.Generated ||
        capability.Permissions.RequiresExplicitApproval ||
        capability.Permissions.Scopes.Contains(CapabilityPermission.SecretsAccess) ||
        capability.Permissions.Scopes.Contains(CapabilityPermission.ProcessExecution);
}
