using System.Diagnostics;
using System.Text.Json;
using SandraMaya.Capabilities;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class CapabilityExecuteTool(
    ICapabilityRegistryService registry,
    ICapabilityExecutionPlanResolver planResolver,
    ICapabilityActivityService activityService,
    StorageLayout storageLayout,
    ILogger<CapabilityExecuteTool> logger) : IToolHandler
{
    private const int DefaultTimeoutSeconds = 60;

    public string Name => "capability_execute";

    public string Description => "Execute an installed capability by ID with optional input parameters";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "capability_id": {
                    "type": "string",
                    "description": "The capability ID to execute"
                },
                "input": {
                    "type": "string",
                    "description": "JSON input to pass to the script via stdin"
                }
            },
            "required": ["capability_id"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var capabilityId = arguments.GetProperty("capability_id").GetString()
                           ?? throw new ArgumentException("capability_id is required");

        string? input = null;
        if (arguments.TryGetProperty("input", out var inputElement))
        {
            input = inputElement.GetString();
        }

        var executionId = Guid.NewGuid().ToString("N");
        var requestedAt = DateTimeOffset.UtcNow;

        try
        {
            var capability = await registry.GetAsync(capabilityId, cancellationToken);
            if (capability is null)
            {
                return ToolResult.Error($"Capability '{capabilityId}' not found.");
            }

            if (capability.Status != CapabilityStatus.Enabled)
            {
                return ToolResult.Error(
                    $"Capability '{capabilityId}' is currently {capability.Status}. " +
                    "It must be approved and enabled before execution. Ask the user to approve it.");
            }

            var plan = await planResolver.ResolveAsync(capabilityId, cancellationToken);

            logger.LogInformation(
                "Executing capability {CapabilityId} (plan: {Command} {Arguments}), containment: {Containment}.",
                capabilityId, plan.Command, string.Join(' ', plan.Arguments), plan.ContainmentProfile);

            var (exitCode, stdout, stderr) = await RunProcessAsync(plan, input, cancellationToken);

            var succeeded = exitCode == 0;
            var status = succeeded ? CapabilityExecutionStatus.Succeeded : CapabilityExecutionStatus.Failed;

            await activityService.RecordExecutionAsync(new CapabilityExecutionRecord(
                ExecutionId: executionId,
                CapabilityId: capabilityId,
                RequestedAt: requestedAt,
                Status: status,
                RequestedBy: context.UserId.ToString(),
                Command: plan.Command,
                WorkingDirectory: plan.WorkingDirectory,
                Arguments: plan.Arguments,
                StartedAt: requestedAt,
                CompletedAt: DateTimeOffset.UtcNow,
                CorrelationId: context.SessionId,
                ContainmentProfile: plan.ContainmentProfile,
                ExitCode: exitCode,
                ErrorMessage: succeeded ? null : stderr),
                cancellationToken);

            if (succeeded)
            {
                var output = string.IsNullOrWhiteSpace(stdout) ? "(no output)" : stdout.Trim();
                return ToolResult.Success(output);
            }

            var errorDetail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return ToolResult.Error($"Capability execution failed (exit code {exitCode}): {errorDetail?.Trim()}");
        }
        catch (CapabilityGovernanceException ex)
        {
            return ToolResult.Error($"Capability governance error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute capability {CapabilityId}.", capabilityId);

            await RecordFailedExecution(executionId, capabilityId, requestedAt, context, ex.Message, cancellationToken);

            return ToolResult.Error($"Failed to execute capability: {ex.Message}");
        }
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
        CapabilityExecutionPlan plan,
        string? input,
        CancellationToken cancellationToken)
    {
        string? inputFilePath = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.Command,
                WorkingDirectory = plan.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = !string.IsNullOrEmpty(input),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in plan.Arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            // If input is provided and large, write to a temp file and pass as argument
            if (!string.IsNullOrEmpty(input) && input.Length > 4096)
            {
                inputFilePath = Path.Combine(storageLayout.TempPath, $"cap-input-{Guid.NewGuid():N}.json");
                Directory.CreateDirectory(storageLayout.TempPath);
                await File.WriteAllTextAsync(inputFilePath, input, cancellationToken);
                startInfo.ArgumentList.Add("--input-file");
                startInfo.ArgumentList.Add(inputFilePath);
                startInfo.RedirectStandardInput = false;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            if (!string.IsNullOrEmpty(input) && input.Length <= 4096)
            {
                await process.StandardInput.WriteAsync(input);
                process.StandardInput.Close();
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(DefaultTimeoutSeconds));

            await process.WaitForExitAsync(timeoutCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            if (inputFilePath != null && File.Exists(inputFilePath))
            {
                try { File.Delete(inputFilePath); } catch { /* best effort */ }
            }
        }
    }

    private async Task RecordFailedExecution(
        string executionId,
        string capabilityId,
        DateTimeOffset requestedAt,
        ToolExecutionContext context,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await activityService.RecordExecutionAsync(new CapabilityExecutionRecord(
                ExecutionId: executionId,
                CapabilityId: capabilityId,
                RequestedAt: requestedAt,
                Status: CapabilityExecutionStatus.Failed,
                RequestedBy: context.UserId.ToString(),
                Command: "unknown",
                WorkingDirectory: storageLayout.WorkPath,
                StartedAt: requestedAt,
                CompletedAt: DateTimeOffset.UtcNow,
                CorrelationId: context.SessionId,
                ErrorMessage: errorMessage),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record execution failure for capability {CapabilityId}.", capabilityId);
        }
    }
}
