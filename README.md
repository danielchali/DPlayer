# DPlayer

A modern, Windows-native desktop media player with a distinctive green-themed visual identity. Built with **.NET 9**, **WPF**, and **LibVLC** for professional-grade playback.

![DPlayer](docs/ui-mockup.svg)

## Features

### Playback
- All common video formats: MP4, MKV, AVI, MOV, WMV, FLV, WEBM, MPEG
- Audio formats: MP3, AAC, FLAC, WAV, OGG, M4A
- Hardware acceleration: NVIDIA NVDEC, AMD AMF, Intel Quick Sync
- 4K/8K and HDR support via LibVLC
- Network streaming: HTTP, HTTPS, RTSP, HLS, DASH
- Playback speeds: 0.25x – 4x, frame-step, AB repeat

### Subtitles
- Automatic subtitle search and download
- Providers: OpenSubtitles, SubDL, Podnapisi
- External subtitle loading (SRT, ASS, SSA, SUB, VTT)
- Delay sync, font/size/color customization

### UI
- Fluent Design / Windows 11 styling with Mica backdrop
- Custom title bar, rounded corners, smooth animations
- Four themes: Dark Green, Emerald Green, Neon Green, Light
- Fullscreen, playlist panel, settings, subtitle search

### Library & Playlists
- Folder scanning and media indexing (SQLite)
- Playlists with import/export (M3U)
- Favorites, watch-later, resume playback
- Scene bookmarks

## Architecture

```
DPlayer/
├── src/
│   ├── DPlayer.App/           # WPF UI (MVVM)
│   ├── DPlayer.Core/          # Domain models & interfaces
│   ├── DPlayer.Infrastructure/# LibVLC, SQLite, subtitle APIs
│   └── DPlayer.Plugins/       # Plugin system
├── tests/
│   └── DPlayer.Core.Tests/
├── installer/
└── docs/
```

- **MVVM** with CommunityToolkit.Mvvm
- **Dependency Injection** via Microsoft.Extensions.Hosting
- **Clean Architecture** — Core has no UI or infrastructure dependencies
- **SQLite** for settings, playlists, library, playback positions
- **Serilog** for file-based logging
- **Modular plugins** — drop DLLs into `%LocalAppData%\DPlayer\Plugins`

## Prerequisites

- Windows 10 (1809+) or Windows 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 17.12+ (optional)

## Build

```powershell
cd DPlayer
dotnet restore
dotnet build --configuration Release
dotnet test
dotnet run --project src/DPlayer.App
```

## Publish

```powershell
.\publish.ps1
```

Self-contained for offline distribution (~230 MB installed). Use `.\publish.ps1 -FrameworkDependent` for a smaller build that requires the .NET 9 Desktop Runtime.

## Configuration

Settings are stored at:
- `%LocalAppData%\DPlayer\settings.json`
- `%LocalAppData%\DPlayer\dplayer.db`
- `%LocalAppData%\DPlayer\logs\`

### OpenSubtitles API

1. Register at [opensubtitles.com](https://www.opensubtitles.com/)
2. Open **Settings → OpenSubtitles API**
3. Enter your API key, username, and password

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Space | Play / Pause |
| F | Fullscreen |
| ← / → | Seek ±10s |
| ↑ / ↓ | Volume |
| Ctrl+O | Open file |
| Ctrl+Shift+O | Open folder |
| Ctrl+S | Subtitle search |
| S | Stop |
| L | Toggle playlist |
| R | Set AB point A |
| Ctrl+Shift+S | Screenshot |

## Installer

See [installer/README.md](installer/README.md) for Inno Setup and WiX configurations.

## Database Schema

See [docs/database-schema.md](docs/database-schema.md).

## License

MIT
