# DPlayer Installer

## Prerequisites

Build a self-contained publish first:

```powershell
dotnet publish src/DPlayer.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o publish
```

Copy the `publish` folder contents to `installer/../publish/` (relative to the `.iss` file).

## Inno Setup (Recommended)

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
2. Open `DPlayer.iss` in Inno Setup Compiler
3. Build → Compile
4. Output: `output/DPlayer-Setup-1.0.0.exe`

### Silent install

```powershell
DPlayer-Setup-1.0.0.exe /VERYSILENT /NORESTART
```

## WiX Toolset (Alternative)

```powershell
# Install WiX: dotnet tool install --global wix
wix build installer/DPlayer.wxs -o output/DPlayer.msi
```

See `DPlayer.wxs` for MSI packaging.

## File Associations

The Inno Setup script registers `.mp4` and `.mkv` when the user selects the file association task during installation.

## Auto-Update

DPlayer checks `https://api.github.com/repos/DPlayer/DPlayer/releases/latest` when enabled in Settings. Replace with your release endpoint.
