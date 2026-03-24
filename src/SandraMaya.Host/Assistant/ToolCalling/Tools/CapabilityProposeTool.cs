using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed partial class CapabilityProposeTool(
    ICapabilityRegistryService registry,
    ICapabilityActivityService activityService,
    ToolRegistry toolRegistry,
    StorageLayout storageLayout,
    ILogger<CapabilityProposeTool> logger) : IToolHandler
{
    private static readonly string[] NetworkPatterns =
        ["http", "https", "fetch", "request", "url", "socket", "axios", "urllib", "httpx", "webclient", "HttpClient"];

    private static readonly string[] ElevatedPatterns =
        ["writeFile", "mkdir", "rmdir", "unlink", "deleteFile", "fs.write", "open(", "shutil", "Remove-Item",
         "New-Item", "Set-Content", "rm ", "cp ", "mv "];

    public string Name => "capability_propose";

    public string Description =>
        "Propose and install a new capability (script) that extends Sandra Maya's abilities. " +
        "The script will be saved and registered as a new tool.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "Capability name in kebab-case, e.g. 'web-scraper-linkedin'"
                },
                "description": {
                    "type": "string",
                    "description": "What this capability does"
                },
                "runtime": {
                    "type": "string",
                    "enum": ["NodeJs", "Python", "PowerShell", "Bash"],
                    "description": "The script runtime"
                },
                "script_content": {
                    "type": "string",
                    "description": "The script source code"
                },
                "parameters_schema": {
                    "type": "string",
                    "description": "Optional JSON Schema for the script's input parameters"
                }
            },
            "required": ["name", "description", "runtime", "script_content"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var name = arguments.GetProperty("name").GetString()
                       ?? throw new ArgumentException("name is required");
            var description = arguments.GetProperty("description").GetString()
                              ?? throw new ArgumentException("description is required");
            var runtimeStr = arguments.GetProperty("runtime").GetString()
                             ?? throw new ArgumentException("runtime is required");
            var scriptContent = arguments.GetProperty("script_content").GetString()
                                ?? throw new ArgumentException("script_content is required");

            if (!Enum.TryParse<CapabilityRuntime>(runtimeStr, ignoreCase: true, out var runtime))
            {
                return ToolResult.Error($"Unsupported runtime: {runtimeStr}. Use NodeJs, Python, PowerShell, or Bash.");
            }

            string? parametersSchema = null;
            if (arguments.TryGetProperty("parameters_schema", out var schemaElement))
            {
                parametersSchema = schemaElement.GetString();
            }

            var capabilityId = GenerateCapabilityId(name);
            var extension = GetFileExtension(runtime);
            var language = GetLanguage(runtime);
            var capabilityDir = Path.Combine(storageLayout.GeneratedCapabilitiesPath, capabilityId);
            var entryPoint = $"main{extension}";
            var scriptPath = Path.Combine(capabilityDir, entryPoint);

            Directory.CreateDirectory(capabilityDir);
            await File.WriteAllTextAsync(scriptPath, scriptContent, cancellationToken);

            if (!string.IsNullOrWhiteSpace(parametersSchema))
            {
                var schemaPath = Path.Combine(capabilityDir, "parameters.schema.json");
                await File.WriteAllTextAsync(schemaPath, parametersSchema, cancellationToken);
            }

            logger.LogInformation(
                "Capability script written to {ScriptPath} for capability {CapabilityId}.",
                scriptPath, capabilityId);

            var containmentProfile = AnalyzeContainment(scriptContent);
            var permissions = BuildPermissionSet(containmentProfile);
            var autoApprove = containmentProfile == "local-readonly";

            var initialStatus = autoApprove ? CapabilityStatus.Enabled : CapabilityStatus.Disabled;
            var initialState = CapabilityState.Installed;

            var runtimeDescriptor = new CapabilityRuntimeDescriptor(
                Language: language,
                Runtime: runtime,
                EntryPoint: entryPoint);

            var request = CapabilityRegistrationRequest.CreateGenerated(
                id: capabilityId,
                name: name,
                version: "1.0.0",
                runtime: runtimeDescriptor,
                installPath: capabilityDir,
                permissions: permissions,
                reference: scriptPath,
                createdBy: context.UserId.ToString(),
                description: description,
                initialStatus: initialStatus,
                initialState: initialState);

            var record = await registry.RegisterAsync(request, cancellationToken);

            await activityService.RecordInstallationAsync(new CapabilityInstallProvenanceRecord(
                InstallId: Guid.NewGuid().ToString("N"),
                CapabilityId: capabilityId,
                InstalledAt: DateTimeOffset.UtcNow,
                InstalledBy: context.UserId.ToString(),
                Source: record.Source,
                InstallPath: capabilityDir,
                PermissionSnapshot: permissions.Scopes,
                Notes: $"Generated via capability_propose tool. Containment: {containmentProfile}"),
                cancellationToken);

            if (autoApprove)
            {
                toolRegistry.Register(new GeneratedCapabilityToolHandler(capabilityId, name, description, parametersSchema));
                logger.LogInformation(
                    "Capability {CapabilityId} auto-approved (local-readonly) and registered as dynamic tool.",
                    capabilityId);
            }

            var result = new
            {
                capabilityId,
                name,
                status = record.Status.ToString(),
                containmentProfile,
                approvalRequired = !autoApprove,
                message = autoApprove
                    ? $"Capability '{name}' installed and enabled. It is now available as a tool."
                    : $"Capability '{name}' installed but requires approval (containment: {containmentProfile}). " +
                      "Ask the user to approve it before it can be executed."
            };

            return ToolResult.Json(result);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to propose capability: {ex.Message}");
        }
    }

    private static string GenerateCapabilityId(string name)
    {
        var kebab = KebabCaseRegex().Replace(name.Trim(), "-").ToLowerInvariant();
        kebab = MultiDashRegex().Replace(kebab, "-").Trim('-');

        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{kebab}-{DateTimeOffset.UtcNow.Ticks}")));

        return $"{kebab}-{hash[..8].ToLowerInvariant()}";
    }

    private static string GetFileExtension(CapabilityRuntime runtime) => runtime switch
    {
        CapabilityRuntime.NodeJs => ".mjs",
        CapabilityRuntime.Python => ".py",
        CapabilityRuntime.PowerShell => ".ps1",
        CapabilityRuntime.Bash => ".sh",
        _ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, "Unsupported runtime for generated capability.")
    };

    private static CapabilityLanguage GetLanguage(CapabilityRuntime runtime) => runtime switch
    {
        CapabilityRuntime.NodeJs => CapabilityLanguage.JavaScript,
        CapabilityRuntime.Python => CapabilityLanguage.Python,
        CapabilityRuntime.PowerShell => CapabilityLanguage.PowerShell,
        CapabilityRuntime.Bash => CapabilityLanguage.Bash,
        _ => CapabilityLanguage.Unknown
    };

    private static string AnalyzeContainment(string scriptContent)
    {
        if (ElevatedPatterns.Any(p => scriptContent.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return "elevated";
        }

        if (NetworkPatterns.Any(p => scriptContent.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return "networked";
        }

        return "local-readonly";
    }

    private static CapabilityPermissionSet BuildPermissionSet(string containmentProfile) => containmentProfile switch
    {
        "elevated" => new CapabilityPermissionSet(
            Scopes: [CapabilityPermission.FileSystemWrite, CapabilityPermission.ProcessExecution],
            RequiresExplicitApproval: true,
            ContainmentBoundary: "elevated"),
        "networked" => new CapabilityPermissionSet(
            Scopes: [CapabilityPermission.NetworkAccess, CapabilityPermission.ExternalApiAccess],
            RequiresExplicitApproval: true,
            ContainmentBoundary: "networked"),
        _ => new CapabilityPermissionSet(
            Scopes: [CapabilityPermission.FileSystemRead],
            ContainmentBoundary: "local-readonly")
    };

    [GeneratedRegex(@"[^a-zA-Z0-9\-]")]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDashRegex();
}

/// <summary>
/// Lightweight tool handler wrapper for dynamically registered generated capabilities.
/// Delegates actual execution to the capability_execute tool.
/// </summary>
internal sealed class GeneratedCapabilityToolHandler : IToolHandler
{
    public string Name { get; }
    public string Description { get; }
    public BinaryData ParametersSchema { get; }

    private readonly string _capabilityId;

    public GeneratedCapabilityToolHandler(string capabilityId, string name, string description, string? parametersSchema)
    {
        _capabilityId = capabilityId;
        Name = $"cap_{name.Replace('-', '_')}";
        Description = description;
        ParametersSchema = BinaryData.FromString(parametersSchema ?? """
            {
                "type": "object",
                "properties": {
                    "input": {
                        "type": "string",
                        "description": "JSON input to pass to the capability"
                    }
                }
            }
            """);
    }

    public Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // This handler acts as a proxy — the orchestrator should invoke capability_execute
        // with the capability ID. For now, return guidance.
        return Task.FromResult(ToolResult.Success(
            $"To execute this capability, use the capability_execute tool with capability_id=\"{_capabilityId}\". " +
            $"Pass any required input as the 'input' parameter."));
    }
}
