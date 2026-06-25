param(
    [string]$Configuration = "Release",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$iss = Join-Path $root "installer\DPlayer.iss"
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Error "Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php"
}

$publishArgs = @()
if ($FrameworkDependent) { $publishArgs += "-FrameworkDependent" }

Write-Host "=== Step 1: Publish ===" -ForegroundColor Green
& (Join-Path $root "publish.ps1") @publishArgs -Configuration $Configuration

$publishDir = Join-Path $root "publish"
$runtimeConfigPath = Join-Path $publishDir "DPlayer.runtimeconfig.json"
$runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json

if (-not $FrameworkDependent) {
    if (-not (Test-Path (Join-Path $publishDir "coreclr.dll"))) {
        Write-Error "Publish is not self-contained (coreclr.dll missing). Installer would require .NET runtime."
    }
    if (-not $runtimeConfig.runtimeOptions.includedFrameworks) {
        Write-Error "Publish is framework-dependent (no includedFrameworks). Run without -FrameworkDependent for offline installs."
    }
    Write-Host "Verified: self-contained publish" -ForegroundColor Cyan
}

Write-Host "`n=== Step 2: Compile installer ===" -ForegroundColor Green
& $iscc $iss

$output = Join-Path $root "output\DPlayer-Setup-1.0.0.exe"
if (-not (Test-Path $output)) {
    Write-Error "Installer not found at $output"
}

$size = (Get-Item $output).Length
Write-Host "`nInstaller ready: $output ($([math]::Round($size / 1MB, 1)) MB)" -ForegroundColor Green
Write-Host "Uninstall any previous DPlayer build before testing." -ForegroundColor Yellow
