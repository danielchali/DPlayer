# PowerShell Script to build the DPlayer installer

$ErrorActionPreference = "Stop"

Write-Host "1. Publishing DPlayer App..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained false -o bin\Release\publish

$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    Write-Error "Inno Setup Compiler (ISCC.exe) was not found at: $isccPath"
}

Write-Host "2. Building Inno Setup Installer..." -ForegroundColor Cyan
& $isccPath dplayer.iss

Write-Host "`nInstaller built successfully!" -ForegroundColor Green
Write-Host "Installer location: C:\Users\Daniel\Documents\GitHub\DPlayer\Installer\DPlayerSetup.exe" -ForegroundColor Green
