# HelloWorldPlugin Quick Start Guide

## Overview

This is the HelloWorldPlugin example that demonstrates how to create plugins for Desktop Clock. It shows the minimal implementation needed to create a working plugin.

## Quick Test Commands

### 1. Build and Run

```bash
# Navigate to this plugin directory
cd "/Users/snqig/Desktop/time/DesktopClock/Plugins/HelloWorldPlugin"

# Build the HelloWorldPlugin.csproj
(Recommended: Use .NET CLI)
# OR Visual Studio Developer Command Prompt:
# dotnet build "./HelloWorldPlugin.csproj" --verbosity minimal

# After building, you can:
# 1. Copy the helloWorldPlugin.dll to the DesktopClock/Plugins directory
# 2. Run DesktopClock.exe
# 3. Open Settings -> Plugins and enable the hello_world plugin
```

### 2. Alternative Copy Method

```bash
# If you want to build from DesktopClock directory:
cd "/Users/snqig/Desktop/time/DesktopClock"

# Copy the compiled HelloWorldPlugin to Plugins directory:
# COPY:
# from: DesktopClock/Plugins/HelloWorldPlugin/bin/Debug/net9.0-windows/HelloWorldPlugin.dll
#   to: DesktopClock/Plugins/HelloWorldPlugin.dll

# Then run DesktopClock.exe
start DesktopClock.exe
```

### 3. Visual Studio

1. Open this directory as a solution in Visual Studio
2. Right-click "HelloWorldPlugin.csproj" and select "Build Solution"
3. Copy the output DLL to DesktopClock/Plugins/

## How HelloWorldPlugin Works

### Plugin Structure

This plugin implements:

1. **IPlugin Interface** - Defines the plugin contract:
   - `Id`: "hello_world"
   - `Name`: "Hello World Plugin"
   - `Version`: "1.0.0"
   - `Description`: Example description

2. **Component Registration** - Automatically registers itself with DesktopClock

3. **UI Display** - Shows a simple Hello World UI with:
   - Title: "Hello World!"
   - Message about the plugin system
   - Draggable component support
   - Real-time uptime display

## Key Features

### ✅ Plugin System Integration

- **Auto-discovery**: PluginManager scans `DesktopClock/Plugins/` directory
- **Runtime Loading**: Plugins loaded when DesktopClock starts
- **Settings Persistence**: Plugin enable/disable states saved

### ✅ UI Component Features

- **Draggable**: Click and drag the component
- **Resizable**: Adjust size as needed
- **Draggable Handle**: "≡ " icon for easy movement
- **Styling**: Modern Neumorphic design
- **Dynamic Content**: Shows uptime and status

### ✅ Development Example

Perfect for learning:
- How to create DesktopClock plugins
- Plugin architecture patterns
- Component registration
- Runtime plugin management

## Testing Steps

### 1. Build
```bash
dotnet build "./HelloWorldPlugin.csproj"
```

### 2. Copy
```bash
cp "bin/Debug/net9.0-windows/HelloWorldPlugin.dll"
   "DesktopClock/Plugins/HelloWorldPlugin.dll"
```

### 3. Run DesktopClock
```bash
start DesktopClock.exe
```

### 4. Enable Plugin
1. Right-click tray icon ► Settings
2. Go to "功能" tab
3. Scroll to "插件" section
4. Check "HelloWorldPlugin"

### 5. Verify
- You should see the HelloWorldPlugin component
- Drag it around the DesktopClock window
- Watch the uptime counter update

## Code Examples

### HelloWorldPlugin.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <Authors>DesktopClock</Authors>
    <Description>Hello World Plugin Example</Description>
  </PropertyGroup>
</Project>
```

### Program.cs
```csharp
namespace HelloWorldPlugin
{
    public class HelloWorldPlugin : IPlugin
    {
        public string Id => "hello_world";
        public string Name => "Hello World Plugin";
        public string Version => "1.0.0";
        public string Description => "Example plugin showing plugin system";

        public void Load(PluginHost host)
        {
            host.Log("Hello World Plugin loaded");
            host.RegisterComponent(Id, new HelloWorldControl());
            host.Log("Hello World component registered");
        }

        public void Unload()
        {
            host.Log("Hello World Plugin unloaded");
        }
    }
}
```

## Requirements

- .NET 9.0 SDK installed
- Visual Studio or .NET CLI
- DesktopClock .NET 9.0 Windows application

## Notes

### First Run

The first time you run this plugin, it will need to be built. The build process may take a few seconds to compile and copy the plugin.

### Dependencies

This plugin does not have any external dependencies. It's a standalone example that demonstrates the plugin system.

### Security

The plugin system uses a sandboxed approach:
- Plugins run in the same process but limited API access
- PluginHost controls component registration
- Invalid/malicious plugins are logged and skipped

## Troubleshooting

### Build Issues

If you encounter build errors:

1. **Clean and rebuild**:
   ```bash
   dotnet clean
   dotnet build "./HelloWorldPlugin.csproj"
   ```

2. **Check for project references**:
   Make sure HelloWorldPlugin.csproj doesn't reference DesktopClock.csproj

3. **Check target framework**:
   Ensure target is `net9.0-windows`

### Runtime Issues

If the plugin doesn't load:

1. **Check file placement**:
   Ensure `HelloWorldPlugin.dll` is in `DesktopClock/Plugins/`

2. **Check settings**:
   Enable the plugin in DesktopClock settings

3. **Check logs**:
   PluginHost logs should show loading messages

### UI Issues

If the UI doesn't display:

1. **Check component registration**:
   Make sure `PluginHost.RegisterComponent()` is called

2. **Check framework elements**:
   Ensure proper `FrameworkElement` types

## Conclusion

This HelloWorldPlugin is a complete, working example of how to:

1. Create a plugin for DesktopClock
2. Integrate with the plugin system
3. Display UI components
4. Handle plugin lifecycle

Use this as a template for creating your own plugins!

## Next Steps

1. **Build and run the plugin**
2. **Try creating your own plugin components**
3. **Explore additional features** in the plugin system
4. **Submit feedback** for improvements

The plugin system is designed to be extensible - create your own plugins and share them!

---

_This plugin is part of the DesktopClock plugin system demonstration_
_See DesktopClock documentation for more plugin development details_
