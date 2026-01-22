# SpaceInvaders

A cross-platform Intel 8080 Space Invaders arcade emulator built with .NET 9 C#.

## Features

- Full Intel 8080 CPU emulation with all 244 opcodes
- SDL2-based hardware-accelerated texture rendering
- Cross-platform audio with SFML.Net
- Accurate Space Invaders arcade hardware simulation
- Color overlay zones (green, red, white) scaled to display size
- Original arcade sound effects
- CRT display effects (linear texture filtering + scanlines)
- **Dynamic runtime window scaling** (1x to 4x) with keyboard controls
- Optimized memory operations using Buffer.BlockCopy
- Async/await architecture with proper cancellation support

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

### Game Controls
- **C** - Insert Coin
- **1** - 1 Player Start
- **2** - 2 Player Start
- **Arrow Keys** - Player 1 Movement (Left/Right)
- **Space** - Player 1 Fire
- **A/D** - Player 2 Movement (Left/Right)
- **W** - Player 2 Fire
- **O/P** - Easter Eggs
- **T** - Tilt

### Display Controls
- **[** - Decrease window scale (minimum 1x = 223x256)
- **]** - Increase window scale (maximum 4x = 892x1024)
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

### CPU Emulation
- **Processor**: Intel 8080 at 2 MHz (original arcade speed)
- **Cycle Accuracy**: Per-instruction cycle counting for precise timing
- **Memory**: 64KB addressable space with 7KB video RAM (0x2400-0x3FFF)
- **Interrupts**: Mid-screen (RST 1) and full-screen (RST 2) interrupts at 60 Hz

### Display System
- **Base Resolution**: 223x256 pixels (original arcade resolution)
- **Scaling**: Dynamic 1x to 4x multiplier (223x256 to 892x1024)
- **Default Scale**: 3x (669x768 window)
- **Refresh Rate**: 60 Hz synchronized with CPU timing
- **Graphics Pipeline**: CPU video RAM → pixel buffer → SDL2 texture → hardware-accelerated rendering
- **Color Zones**: Authentic arcade color overlay (green bottom, red top, white middle)
- **CRT Effects**: Linear texture filtering with scanlines (alpha 90, spacing scales with window size)
- **Pixel Format**: ARGB8888 (32-bit color)

### Audio System
- **Engine**: SFML.Net with sound caching
- **Files**: Original arcade WAV files
- **Platform**: Cross-platform (Windows/macOS/Linux)
- **Fallback**: Silent mode if audio unavailable

### Performance Optimizations
- **Memory Operations**: Buffer.BlockCopy for 10-15% improvement over Array.Copy
- **Texture Rendering**: Single texture update per frame (50-70% improvement over rect fills)
- **Frame Timing**: Task.Delay with CancellationToken for precise 16.7ms frame timing
- **Thread Safety**: Lock-based synchronization for runtime resource recreation

### Threading Architecture
- **CPU Thread**: Executes 8080 instructions with cycle-accurate timing
- **Display Thread**: SDL2 rendering at 60 Hz with CRT effects
- **Sound Thread**: Audio event processing with sound caching
- **Port Thread**: Input port synchronization
- **Main Thread**: SDL event loop and keyboard input handling
