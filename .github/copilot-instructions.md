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
- C = Coin, 1/2 = Start, Arrows = Move, Space = Fire, ESC = Exit
- Full keyboard mapping in `Cabinet.GetKeyValue()`

## Display System
- Resolution: 446x512 (2x scaled from 223x256)
- Color Zones: Green (bottom), Red (top), White (middle)
- Rendering: SDL2 hardware-accelerated with per-pixel color mapping
- Refresh: 60 Hz matching original arcade hardware

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

## Threading Model
- CPU Thread: Executes 8080 instructions
- Display Thread: SDL2 rendering at 60 Hz
- Sound Thread: Audio event processing
- Port Thread: Input port synchronization
- Main Thread: SDL event loop and input handling
