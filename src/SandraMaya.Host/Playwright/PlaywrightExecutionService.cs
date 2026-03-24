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

            // Determine a working directory where Node can resolve 'playwright' (look for node_modules/playwright)
            string ResolveWorkingDir()
            {
                var candidates = new[] {
                    AppContext.BaseDirectory,
                    Environment.CurrentDirectory,
                    _storage.WorkPath,
                    _storage.Root,
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")),
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."))
                };

                foreach (var c in candidates)
                {
                    if (string.IsNullOrWhiteSpace(c)) continue;
                    try
                    {
                        var nm = Path.Combine(c, "node_modules", "playwright");
                        if (Directory.Exists(nm)) return c;
                    }
                    catch { /* ignore IO errors */ }
                }

                // Fallback to the script folder so Node will at least resolve relative imports there
                return Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory;
            }

            var workingDir = ResolveWorkingDir();

            // Attempt to run the script, and if it fails due to missing browsers try a one-time automatic install and retry.
            async Task<PlaywrightScriptResult> RunProcessAsync()
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _runtime.PlaywrightCommand,
                    Arguments = $"\"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                };

                // Ensure Playwright browsers path is set
                if (!psi.Environment.ContainsKey("PLAYWRIGHT_BROWSERS_PATH"))
                {
                    psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] =
                        Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? "0";
                }

                // Help Node resolve packages by setting NODE_PATH when node_modules exists next to workingDir
                try
                {
                    var nodeModules = Path.Combine(workingDir, "node_modules");
                    if (Directory.Exists(nodeModules) && !psi.Environment.ContainsKey("NODE_PATH"))
                    {
                        psi.Environment["NODE_PATH"] = nodeModules;
                    }
                }
                catch { /* ignore */ }

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

            // First attempt
            var result = await RunProcessAsync();

            // If it failed due to missing browser executables, try installing chromium once and retry.
            if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.ErrorOutput))
            {
                var errLower = result.ErrorOutput.ToLowerInvariant();
                if (errLower.Contains("executable doesn't exist") || errLower.Contains("chrome-headless-shell") || errLower.Contains("browser executable") || errLower.Contains("playwright install"))
                {
                    _logger.LogInformation("Detected missing Playwright browsers, attempting to install chromium via npx.");

                    try
                    {
                        var installPsi = new ProcessStartInfo
                        {
                            FileName = "npx",
                            Arguments = "playwright install chromium",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = workingDir
                        };

                        // Propagate environment hints
                        if (!installPsi.Environment.ContainsKey("PLAYWRIGHT_BROWSERS_PATH") && psiEnvironmentHasKey("PLAYWRIGHT_BROWSERS_PATH", out var val))
                        {
                            installPsi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = val;
                        }

                        using var installProcess = Process.Start(installPsi);
                        if (installProcess is not null)
                        {
                            var outTask = installProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                            var errTask = installProcess.StandardError.ReadToEndAsync(cancellationToken);
                            await installProcess.WaitForExitAsync(cancellationToken);
                            var outStr = await outTask;
                            var errStr = await errTask;

                            if (installProcess.ExitCode == 0)
                            {
                                _logger.LogInformation("Playwright browsers installed successfully. Retrying script.");
                                // Retry the script once
                                result = await RunProcessAsync();
                            }
                            else
                            {
                                _logger.LogWarning("Playwright install failed: {Err}\n{Out}", errStr, outStr);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to run npx playwright install chromium");
                    }
                }
            }

            return result;

            // Local helper to read existing environment key if present on the process start info
            bool psiEnvironmentHasKey(string key, out string? value)
            {
                value = null;
                try
                {
                    var nodeModules = Path.Combine(workingDir, "node_modules");
                    var envPath = Environment.GetEnvironmentVariable(key);
                    if (!string.IsNullOrEmpty(envPath))
                    {
                        value = envPath;
                        return true;
                    }
                }
                catch { }

                return false;
            }
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
