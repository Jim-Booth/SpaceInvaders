# SpaceInvaders - .NET 9 C# Intel 8080 Emulator

This is a cross-platform Intel 8080 Space Invaders arcade emulator built with .NET 9 C#.

## Project Type
- Language: C#
- Framework: .NET 9
- Project Type: Console Application (with SDL2 GUI)
- Architecture: Intel 8080 CPU Emulator

## Build and Run
- Build: `dotnet build`
- Run: `dotnet run`
- Prerequisites: SDL2 library must be installed (`brew install sdl2` on macOS)

## Key Technologies
- **SDL2-CS**: Graphics rendering and keyboard input
- **SFML.Net**: Cross-platform audio playback
- **System.Drawing.Common**: Color definitions
- **Unsafe Code**: Enabled for SDL2 P/Invoke calls

## Project Files
- [Program.cs](../Program.cs) - Main entry point
- [Cabinet.cs](../Cabinet.cs) - Arcade cabinet simulation, SDL2 rendering, input handling
- [SDL2.cs](../SDL2.cs) - SDL2 C# bindings (source-only package)
- [LPUtf8StrMarshaler.cs](../LPUtf8StrMarshaler.cs) - SDL2 string marshaling
- [SpaceInvaders.csproj](../SpaceInvaders.csproj) - Project configuration

## Intel 8080 Emulation (MAINBOARD/)
- [Intel_8080.cs](../MAINBOARD/Intel_8080.cs) - CPU core with all 8080 opcodes
- [Memory.cs](../MAINBOARD/Memory.cs) - 64KB addressable memory
- [Registers.cs](../MAINBOARD/Registers.cs) - CPU registers (A, B, C, D, E, H, L, PC, SP)
- [Flags.cs](../MAINBOARD/Flags.cs) - Status flags (Z, S, P, CY, AC)
- [Audio.cs](../MAINBOARD/Audio.cs) - SFML audio playback engine

## Game Controls
- **C** = Insert Coin
- **1** = 1P Start
- **2** = 2P Start
- **Arrow Keys** = Move (1P)
- **Space** = Fire (1P)
- **A/D** = Move (2P)
- **W** = Fire (2P)
- **ESC** = Exit
- **[** = Decrease scale (2x-4x)
- **]** = Increase scale (2x-4x)
- **B** = Toggle background texture

Full keyboard mapping in `Cabinet.GetKeyValue()`

## Display System
- Resolution: 669x768 default (3x scaled from 223x256)
- Scalable: 2x to 4x multiplier ([ and ] keys)
- Color Zones: Green (player area), Red (UFO area), White (middle)
- Background: Optional Cabinet.bmp texture with alpha blending
- CRT Effect: Scanline overlay for authentic appearance
- Rendering: SDL2 hardware-accelerated with per-pixel color mapping
- Refresh: 60 Hz matching original arcade hardware

## Background Texture
- File: `Cabinet.bmp` in application directory
- Format: BMP (SDL2 native format)
- Toggle: Press 'B' key at runtime to show/hide
- Blending: Game pixels render with alpha transparency over background

## Audio System
- Engine: SFML.Net with sound caching
- Files: WAV files in SOUNDS/ directory
- Platform: Cross-platform (Windows/macOS/Linux)
- Fallback: Silent mode if audio unavailable

## ROM Files
Located in ROMS/ directory:
- invaders.h (0x0000-0x07FF)
- invaders.g (0x0800-0x0FFF)
- invaders.f (0x1000-0x17FF)
- invaders.e (0x1800-0x1FFF)

## Resource Files (Auto-copied on Build)
The following are automatically copied to the output directory:
- `Cabinet.bmp` - Background texture
- `SOUNDS/` - Audio files directory
- `ROMS/` - ROM files directory

## Threading Model
- CPU Thread: Executes 8080 instructions at highest priority
- Display Thread: SDL2 rendering at 60 Hz with resize lock
- Sound Thread: Audio event processing
- Port Thread: Input port synchronization
- Main Thread: SDL event loop and input handling
