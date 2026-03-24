using System.Diagnostics;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Playwright;

public sealed record PlaywrightScriptRequest
{
    public required string Script { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
}

public sealed record PlaywrightScriptResult
{
    public bool Succeeded { get; init; }
    public string Output { get; init; } = string.Empty;
    public string ErrorOutput { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}

public interface IPlaywrightExecutionService
{
    Task<PlaywrightScriptResult> ExecuteScriptAsync(
        PlaywrightScriptRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PlaywrightExecutionService : IPlaywrightExecutionService
{
    private readonly RuntimeOptions _runtime;
    private readonly StorageLayout _storage;
    private readonly ILogger<PlaywrightExecutionService> _logger;

    public PlaywrightExecutionService(
        IOptions<RuntimeOptions> runtimeOptions,
        StorageLayout storage,
        ILogger<PlaywrightExecutionService> logger)
    {
        _runtime = runtimeOptions.Value;
        _storage = storage;
        _logger = logger;
    }

    public async Task<PlaywrightScriptResult> ExecuteScriptAsync(
        PlaywrightScriptRequest request,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storage.TempPath);

        var scriptFileName = $"pw-{Guid.NewGuid():N}.mjs";
        var scriptPath = Path.Combine(_storage.TempPath, scriptFileName);

        try
        {
            await File.WriteAllTextAsync(scriptPath, request.Script, cancellationToken);

            _logger.LogDebug(
                "Executing Playwright script {ScriptFile} with timeout {Timeout}s",
                scriptFileName,
                request.Timeout.TotalSeconds);

            var psi = new ProcessStartInfo
            {
                FileName = _runtime.PlaywrightCommand,
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            if (!psi.Environment.ContainsKey("PLAYWRIGHT_BROWSERS_PATH"))
            {
                psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] =
                    Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? "0";
            }

            if (request.EnvironmentVariables is not null)
            {
                foreach (var (key, value) in request.EnvironmentVariables)
                {
                    psi.Environment[key] = value;
                }
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(request.Timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Playwright script {ScriptFile} timed out after {Timeout}s — killing process",
                    scriptFileName,
                    request.Timeout.TotalSeconds);

                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* already exited */ }

                return new PlaywrightScriptResult
                {
                    Succeeded = false,
                    Output = string.Empty,
                    ErrorOutput = $"Script timed out after {request.Timeout.TotalSeconds} seconds.",
                    ExitCode = -1
                };
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            _logger.LogDebug(
                "Playwright script {ScriptFile} exited with code {ExitCode}",
                scriptFileName,
                process.ExitCode);

            return new PlaywrightScriptResult
            {
                Succeeded = process.ExitCode == 0,
                Output = stdout,
                ErrorOutput = stderr,
                ExitCode = process.ExitCode
            };
        }
        finally
        {
            try { File.Delete(scriptPath); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp script {ScriptFile}", scriptPath);
            }
        }
    }
}
