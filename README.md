# SpaceInvaders

A cross-platform Intel 8080 Space Invaders arcade emulator built with .NET 9 C#.

## Features

- Full Intel 8080 CPU emulation
- SDL2-based graphics rendering
- Cross-platform audio with SFML.Net
- Accurate Space Invaders arcade hardware simulation
- Color overlay zones (green, red, white)
- Original sound effects
- Keyboard controls

## Requirements

- .NET 9 SDK
- SDL2 library (installed via Homebrew on macOS: `brew install sdl2`)

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run
```

## Controls

- **C** - Insert Coin
- **1** - 1 Player Start
- **2** - 2 Player Start
- **Arrow Keys** - Player 1 Movement (Left/Right)
- **Space** - Player 1 Fire
- **A/D** - Player 2 Movement (Left/Right)
- **W** - Player 2 Fire
- **O/P** - Easter Eggs
- **T** - Tilt
- **ESC** - Exit Game

## Project Structure

- `Program.cs` - Main entry point
- `Cabinet.cs` - Arcade cabinet simulation and main game loop
- `SDL2.cs` - SDL2 bindings for graphics
- `LPUtf8StrMarshaler.cs` - SDL2 string marshaling
- `SpaceInvaders.csproj` - Project configuration
- `MAINBOARD/` - Intel 8080 CPU emulation
  - `Intel_8080.cs` - CPU implementation
  - `Memory.cs` - Memory management
  - `Registers.cs` - CPU registers
  - `Flags.cs` - CPU flags
  - `Audio.cs` - Audio playback engine
- `ROMS/` - Space Invaders ROM files
- `SOUNDS/` - Game sound effects

## Dependencies

- **SDL2-CS** (2.0.0) - SDL2 bindings for .NET
- **SFML.Net** (3.0.0) - Cross-platform audio
- **System.Drawing.Common** (10.0.2) - Graphics support

## Platform Notes

### macOS

1. Install SDL2: `brew install sdl2`
2. The SDL2 library will be automatically copied to the build output
3. Audio works out of the box with SFML.Net

### Windows

SDL2.dll and SFML audio libraries are included in the NuGet packages.

### Linux

Install SDL2 via your package manager (e.g., `sudo apt install libsdl2-2.0-0`).

## Technical Details

- **CPU**: Intel 8080 emulation running at original arcade speed
- **Display**: 224x256 rotated display (rendered as 446x512)
- **Refresh Rate**: 60 Hz
- **Audio**: Original arcade sound effects via SFML.Net
- **Graphics**: SDL2 hardware-accelerated rendering
