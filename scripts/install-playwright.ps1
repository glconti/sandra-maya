Param(
    [string]$WorkingDir = (Get-Location).Path
)

Push-Location $WorkingDir
try {
    if (-not (Test-Path package.json)) {
        Write-Error "package.json not found in $WorkingDir. Run this from repository root or specify the path."
        exit 1
    }

    Write-Host "Running npm install in $WorkingDir..."
    npm install

    Write-Host "Installing Playwright browsers (chromium)..."
    npx playwright install chromium
}
finally {
    Pop-Location
}
