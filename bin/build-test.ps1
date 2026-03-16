[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

Push-Location (Resolve-Path "$PSScriptRoot\..")
try {
    dotnet build KoreForge.Settings.sln --force -c $Configuration
    dotnet test  KoreForge.Settings.sln -c $Configuration --no-build `
        --logger "html;LogFileName=TestResults.html" `
        --results-directory out/TestResults
    Write-Host 'Test results: out/TestResults/TestResults.html' -ForegroundColor Green
} finally {
    Pop-Location
}