param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "publish",
    [switch]$FrameworkDependent,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $root $Output
$project = Join-Path $root "src\DPlayer.App\DPlayer.App.csproj"
$vlcNuget = Join-Path $env:USERPROFILE ".nuget\packages\videolan.libvlc.windows\3.0.21\build\x64"

function Format-Mb([long]$Bytes) {
  if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
  return "{0:N1} MB" -f ($Bytes / 1MB)
}

function Get-DirSize([string]$Path) {
  if (-not (Test-Path $Path)) { return 0 }
  return (Get-ChildItem $Path -Recurse -File -ErrorAction SilentlyContinue |
    Measure-Object -Property Length -Sum).Sum
}

function Show-FolderBreakdown([string]$Path, [string]$Label, [int]$Top = 12) {
  if (-not (Test-Path $Path)) { return }

  Write-Host "`n$Label" -ForegroundColor Yellow
  Get-ChildItem $Path -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $size = Get-DirSize $_.FullName
    [PSCustomObject]@{ Name = $_.Name; Bytes = $size }
  } | Sort-Object Bytes -Descending | Select-Object -First $Top | ForEach-Object {
    Write-Host ("  {0,-28} {1}" -f $_.Name, (Format-Mb $_.Bytes))
  }
}

Push-Location $root
try {
  dotnet --version | Out-Null
}
catch {
  Write-Error ".NET 9 SDK is required. Install from https://dotnet.microsoft.com/download/dotnet/9.0"
}

Write-Host "=== DPlayer Publish ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration"
Write-Host "Runtime:       $Runtime"
Write-Host "Output:        $publishDir"
Write-Host "Self-contained: $(-not $FrameworkDependent)"

if (-not $SkipBuild) {
  if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
  }

  $publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $publishDir,
    "-p:PublishSingleFile=false",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
  )
  if (-not $FrameworkDependent) {
    $publishArgs += "--self-contained"
    $publishArgs += "-p:SelfContained=true"
  }

  dotnet @publishArgs
}

if (-not $FrameworkDependent) {
  $runtimeConfigPath = Join-Path $publishDir "DPlayer.runtimeconfig.json"
  if (-not (Test-Path (Join-Path $publishDir "coreclr.dll"))) {
    Write-Error "Publish failed self-contained check: coreclr.dll missing from $publishDir"
  }
  if (Test-Path $runtimeConfigPath) {
    $runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
    if (-not $runtimeConfig.runtimeOptions.includedFrameworks) {
      Write-Error "Publish is framework-dependent. Use build-installer.ps1 or omit -FrameworkDependent for offline distribution."
    }
  }
}

if (-not (Test-Path $publishDir)) {
  Write-Error "Publish output not found at $publishDir"
}

$total = Get-DirSize $publishDir
$vlcPath = Join-Path $publishDir "libvlc\win-x64"
$vlcSize = Get-DirSize $vlcPath
$appSize = $total - $vlcSize
$fullVlcSize = Get-DirSize $vlcNuget
$vlcSaved = [Math]::Max(0, $fullVlcSize - $vlcSize)

Write-Host "`n=== Publish size ===" -ForegroundColor Green
Write-Host ("  Total:              {0}" -f (Format-Mb $total))
Write-Host ("  LibVLC (trimmed):   {0}" -f (Format-Mb $vlcSize)) -ForegroundColor Cyan
if (-not $FrameworkDependent) {
  Write-Host ("  App + .NET runtime: {0}" -f (Format-Mb $appSize))
} else {
  Write-Host ("  App only:           {0}" -f (Format-Mb $appSize)) -ForegroundColor Cyan
  Write-Host "  Requires .NET 9 Desktop Runtime on target machines" -ForegroundColor DarkGray
}

if ($fullVlcSize -gt 0) {
  Write-Host "`n=== LibVLC comparison (x64 NuGet baseline vs publish) ===" -ForegroundColor Green
  Write-Host ("  Full package:       {0}" -f (Format-Mb $fullVlcSize))
  Write-Host ("  In publish:         {0}" -f (Format-Mb $vlcSize))
  Write-Host ("  Saved by excludes:  {0}" -f (Format-Mb $vlcSaved)) -ForegroundColor Cyan
}

Show-FolderBreakdown $publishDir "Top-level folders:" 10
Show-FolderBreakdown (Join-Path $vlcPath "plugins") "LibVLC plugin categories:" 15

$fileCount = (Get-ChildItem $publishDir -Recurse -File).Count
Write-Host "`nFiles: $fileCount" -ForegroundColor DarkGray
Write-Host "Next: .\build-installer.ps1  (or compile installer/DPlayer.iss in Inno Setup)" -ForegroundColor Green

Pop-Location
