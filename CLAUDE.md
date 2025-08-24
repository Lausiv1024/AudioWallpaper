# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AudioWallpaper is a Windows desktop application that creates real-time audio visualizations. It captures system audio output and displays it as an animated spectrum analyzer using WPF and DirectX.

**Technology Stack:**
- .NET Framework 4.7.2
- C# / WPF (Windows Presentation Foundation)
- NAudio 2.1.0 for audio capture
- SharpDX 4.2.0 for DirectX integration
- MathNet.Numerics 5.0.0 for FFT calculations

## Build Commands

This is a Visual Studio solution. Build using:

```bash
# Restore NuGet packages
nuget restore AudioWallpaper.sln

# Build Debug configuration
msbuild AudioWallpaper.sln /p:Configuration=Debug

# Build Release configuration
msbuild AudioWallpaper.sln /p:Configuration=Release
```

Or open `AudioWallpaper.sln` in Visual Studio and use the Build menu.

## Architecture Overview

### Core Components

1. **Audio Capture Pipeline** (MainWindow.xaml.cs:95-134)
   - Uses WASAPI Loopback to capture system audio
   - Processes at 24kHz sample rate in mono
   - Implements real-time FFT with 512-sample window

2. **Visualization System** (MainWindow.xaml.cs:136-188)
   - Creates 128 frequency bars using VisualizerRect objects
   - Updates at 60/30 FPS via DispatcherTimer
   - Each bar height represents frequency magnitude

3. **Signal Processing** (MainWindow.xaml.cs:189-225)
   - Applies Hamming window before FFT
   - Converts complex FFT output to magnitude values
   - Maps frequency bins to visual elements

### Key Files

- `MainWindow.xaml.cs` - Main application logic, audio capture, FFT processing
- `VisualizerRect.cs` - Individual spectrum bar component
- `DirectXManager.cs` - Placeholder for DirectX rendering (not implemented)
- `InteropModule.cs` - P/Invoke declarations for Windows API
- `Buffer1.cs` - Ring buffer implementation (currently unused)

### Window Controls

- **ESC** - Close application
- **F/F11** - Toggle fullscreen
- **Mouse drag** - Move window
- **Mouse wheel** - Test visualization (debug)

## Development Notes

- The codebase contains Japanese comments throughout
- DirectX integration is referenced but not fully implemented
- Desktop wallpaper functionality appears planned but not implemented
- No unit tests are present in the project

## Common Tasks

To add new visualization effects:
1. Modify the FFT processing in `MainWindow.xaml.cs:DataAvailable`
2. Update rectangle animations in the DispatcherTimer callback
3. Consider implementing the DirectXManager for GPU-accelerated rendering

To debug audio capture issues:
- Check the debug text output showing data availability rate
- Verify WASAPI device initialization in `MainWindow_Loaded`
- Monitor the `DataAvailable` event handler for audio buffer processing