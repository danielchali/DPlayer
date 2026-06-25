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

### Framework-dependent

```powershell
dotnet publish src/DPlayer.App -c Release -o ./publish
```

Requires .NET 9 Desktop Runtime on target machines.

### Self-contained (recommended for distribution)

```powershell
dotnet publish src/DPlayer.App -c Release -r win-x64 --self-contained -o ./publish
```

### Installer

1. Publish self-contained build to `publish/`
2. Compile `installer/DPlayer.iss` with Inno Setup
3. Distribute `output/DPlayer-Setup-1.0.0.exe`

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
