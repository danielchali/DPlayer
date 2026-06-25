# DPlayer Plugin Development

## Overview

DPlayer supports external plugins via DLLs placed in:

```
%LocalAppData%\DPlayer\Plugins\
```

## Creating a Plugin

### 1. Create a class library

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\src\DPlayer.Plugins\DPlayer.Plugins.csproj" />
  </ItemGroup>
</Project>
```

### 2. Implement IDPlayerPlugin

```csharp
using DPlayer.Plugins;

public class MyPlugin : IDPlayerPlugin
{
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public string Description => "Example DPlayer plugin";

    public void Initialize(IPluginContext context)
    {
        context.Log("MyPlugin loaded");
        context.RegisterMenuItem("Tools/My Action", () => { });
    }

    public void Shutdown() { }
}
```

### 3. Subtitle Provider Plugin

Implement `ISubtitleProviderPlugin` for custom subtitle sources:

```csharp
public class MySubtitlePlugin : ISubtitleProviderPlugin
{
    public string ProviderId => "mysubtitles";
    // ... implement SearchAsync and DownloadAsync
}
```

### 4. Deploy

Build the plugin and copy the DLL (and dependencies) to the Plugins folder. Restart DPlayer.

## Plugin Context API

| Method | Description |
|--------|-------------|
| `GetService<T>()` | Resolve a registered DI service |
| `RegisterMenuItem(path, action)` | Add context menu entry |
| `Log(message)` | Write to application log |
| `DataDirectory` | App data folder path |
