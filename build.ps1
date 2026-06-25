param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=== DPlayer Build ===" -ForegroundColor Green

Push-Location $root
try {
    dotnet --version | Out-Null
}
catch {
    Write-Error ".NET 9 SDK is required. Install from https://dotnet.microsoft.com/download/dotnet/9.0"
}

dotnet restore DPlayer.sln
dotnet build DPlayer.sln -c $Configuration --no-restore
dotnet test tests/DPlayer.Core.Tests -c $Configuration --no-build

Write-Host "`nBuild complete. Run with:" -ForegroundColor Green
Write-Host "  dotnet run --project src/DPlayer.App" -ForegroundColor Cyan

Pop-Location
