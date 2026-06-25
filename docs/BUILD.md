# Building and Deployment

## Development Setup

1. Install [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Clone the repository
3. Run `.\build.ps1` or:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/DPlayer.App
```

## Project Structure

| Project | Purpose |
|---------|---------|
| `DPlayer.App` | WPF shell, views, view models, themes |
| `DPlayer.Core` | Domain models, enums, service interfaces |
| `DPlayer.Infrastructure` | LibVLC player, SQLite, subtitle APIs, logging |
| `DPlayer.Plugins` | Plugin loader and contracts |
| `DPlayer.Core.Tests` | Unit tests |

## Media Engine

DPlayer uses **LibVLC 3.x** via LibVLCSharp for decoding and rendering:

- Hardware decode: `--avcodec-hw=any` (NVDEC, AMF, Quick Sync)
- GPU output: `--vout=direct3d11`
- Formats: all LibVLC-supported containers and codecs
- Streaming: pass URL to `LoadStreamAsync`

Native LibVLC binaries are restored automatically via the `VideoLAN.LibVLC.Windows` NuGet package.

## Deployment

### Self-contained (recommended for distribution / offline)

```powershell
.\publish.ps1
```

Bundles the .NET 9 runtime (~230 MB installed). No prerequisites on target machines.

### Framework-dependent (smaller)

```powershell
.\publish.ps1 -FrameworkDependent
```

~90–100 MB installed. Requires [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) on target machines.

Equivalent:

```powershell
dotnet publish src/DPlayer.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o ./publish
```

### Installer

```powershell
.\build-installer.ps1
```

This publishes a self-contained build and compiles `installer/DPlayer.iss` in one step. Manual alternative:

1. Run `.\publish.ps1` to build `publish/`
2. Compile `installer/DPlayer.iss` with Inno Setup
3. Distribute `output/DPlayer-Setup-1.0.0.exe`

Uninstall any previous DPlayer build before installing a new one.

## Plugin Development

1. Create a class library targeting `net9.0`
2. Reference `DPlayer.Plugins`
3. Implement `IDPlayerPlugin` or `ISubtitleProviderPlugin`
4. Build and copy DLL to `%LocalAppData%\DPlayer\Plugins\`

## Logging

Logs are written to `%LocalAppData%\DPlayer\logs\dplayer-YYYYMMDD.log` via Serilog.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Black video screen | Ensure GPU drivers are updated; try disabling HW accel in Settings |
| No subtitles from OpenSubtitles | Add API key in Settings |
| LibVLC not found | Run `dotnet restore` to fetch native binaries |
| Mica not visible | Requires Windows 11; falls back gracefully on Windows 10 |
