param(
    [string]$ProjectPath = "src/SandraMaya.Host/SandraMaya.Host.csproj",
    [switch]$InstallPlaywrightBrowsers
)

$dockerArgs = @("build", "-t", "sandra-maya:dev")

if ($ProjectPath) {
    $dockerArgs += "--build-arg"
    $dockerArgs += "PROJECT_PATH=$ProjectPath"
}

$dockerArgs += "--build-arg"
$dockerArgs += "INSTALL_PLAYWRIGHT_BROWSERS=$($InstallPlaywrightBrowsers.IsPresent.ToString().ToLowerInvariant())"
$dockerArgs += "."

docker @dockerArgs
