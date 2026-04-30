[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

Push-Location (Resolve-Path "$PSScriptRoot\..")
try {
    dotnet build KoreForge.Settings.slnx --force -c $Configuration
} finally {
    Pop-Location
}