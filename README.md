# Space Invaders - Intel 8080 Emulator

A cross-platform Intel 8080 Space Invaders arcade emulator built with .NET 9 and C#.

![Space Invaders](https://img.shields.io/badge/.NET-9.0-512BD4) ![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)

## Features

- **Accurate Intel 8080 CPU emulation** - Full implementation of all 8080 opcodes
- **Authentic display rendering** - Color zones matching original arcade cabinet (green, red, white)
- **CRT effects** - Authentic arcade monitor simulation (Default ON):
  - Horizontal and vertical scanlines
  - Rounded corners (barrel distortion)
  - Vignette edge darkening
  - Phosphor persistence (ghosting trails on moving objects)
  - Horizontal blur (electron beam spread)
  - Screen flicker (subtle brightness variation)
  - Random horizontal jitter (signal instability)
  - Power-on warmup (gradual brightness increase)
- **Authentic audio** - Low-pass filtered sound effects simulating arcade cabinet speakers
- **DIP switch emulation** - Configurable lives, bonus life threshold, and coin info display with persistent settings
- **High score persistence** - High scores are saved to `settings.json` and restored on startup
- **Background texture support** - Overlay game on custom cabinet artwork
- **Scalable display** - 1x to 4x resolution scaling (Default is 2x)
- **Cross-platform** - Runs on Windows, macOS, and Linux

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SDL2 library
  - **macOS**: `brew install sdl2`
  - **Linux**: `sudo apt install libsdl2-dev`
  - **Windows**: SDL2.dll included or download from [libsdl.org](https://www.libsdl.org/)

## Build and Run

```bash
dotnet build
dotnet run
```

## Controls

### Game Controls

| Key | Action |
|-----|--------|
| **C** | Insert Coin |
| **1** | 1 Player Start |
| **2** | 2 Player Start |
| **←** / **→** | Move (Player 1) |
| **Space** | Fire (Player 1) |
| **A** / **D** | Move (Player 2) |
| **W** | Fire (Player 2) |
| **T** | Tilt (ends current game) |
| **P** | Pause game |
| **ESC** | Exit |

### Display Controls

| Key | Action |
|-----|--------|
| **[** / **]** | Decrease / Increase scale (2x-4x) |
| **B** | Toggle background texture |
| **R** | Toggle CRT effects (including phosphor persistence) |
| **S** | Toggle sound on/off |
| **F** | Toggle FPS counter |

### DIP Switch Controls

| Key | Action |
|-----|--------|
| **F1** | Cycle lives (3 → 4 → 5 → 6) |
| **F2** | Toggle bonus life threshold (1500 / 1000) |
| **F3** | Toggle coin info display |

## DIP Switches

The original Space Invaders arcade PCB featured physical DIP (Dual In-line Package) switches - small toggle switches that arcade operators would configure before powering on the machine. These were used to adjust game difficulty and revenue settings. This emulator lets you modify these settings via function keys for a customized experience.

Settings are saved to `settings.json` and persist between sessions.

| Setting | Options | Default | Key |
|---------|---------|---------|-----|
| **Lives** | 3, 4, 5, or 6 | 3 | F1 |
| **Bonus Life** | 1500 or 1000 points | 1500 | F2 |
| **Coin Info** | Show or hide in demo | Show | F3 |

Changes are saved automatically.

**Note:** The lives setting only displays correctly during active gameplay. During demo/attraction mode, the change may not be visible until a new game is started.

## ROM Files

Place the following ROM files in the `ROMS/` directory:

| File | Address Range |
|------|---------------|
| `invaders.h` | 0x0000 - 0x07FF |
| `invaders.g` | 0x0800 - 0x0FFF |
| `invaders.f` | 0x1000 - 0x17FF |
| `invaders.e` | 0x1800 - 0x1FFF |

## Sound Files

Place WAV sound files in the `SOUNDS/` directory:

- `ufo_lowpitch.wav`
- `shoot.wav`
- `explosion.wav`
- `invaderkilled.wav`
- `fastinvader1.wav` - `fastinvader4.wav`
- `extendedPlay.wav`

## Background Texture

Place a `Cabinet.bmp` file in the application directory to display a background image behind the game. Press **B** to toggle visibility at runtime.

## Project Structure

```
SpaceInvaders/
├── Program.cs              # Entry point
├── Cabinet.cs              # Arcade cabinet simulation, SDL2 rendering
├── MAINBOARD/
│   ├── Intel8080.cs        # CPU emulation core
│   ├── Memory.cs           # 64KB addressable memory
│   ├── Registers.cs        # CPU registers
│   ├── Flags.cs            # Status flags (Z, S, P, CY, AC)
│   └── Audio.cs            # SFML audio playback
├── ROMS/                   # ROM files (not included)
├── SOUNDS/                 # Sound effect WAV files
└── Cabinet.bmp             # Optional background texture
```

## Technical Details

- **CPU Clock**: 2 MHz emulated
- **Display**: 224x256 rotated 90° (renders as 256x224)
- **Refresh Rate**: 60 Hz with mid-screen and full-screen interrupts
- **Color Overlay**: Simulates original arcade color gel overlay
  - Green: Player and shields area
  - Red: UFO area at top
  - White: Middle play area

## CRT Simulation

The emulator includes authentic CRT monitor effects to recreate the arcade experience:

| Effect | Description |
|--------|-------------|
| **Scanlines** | Horizontal and vertical lines simulating CRT raster |
| **Vignette** | Edge darkening with quadratic falloff from center |
| **Rounded Corners** | Barrel distortion mask simulating curved CRT glass |
| **Phosphor Persistence** | 75% decay rate creating ghosting trails on moving sprites |
| **Horizontal Blur** | 30% blend with adjacent pixels simulating electron beam spread |
| **Screen Flicker** | 2% random brightness variation per frame |
| **Horizontal Jitter** | Rare random horizontal displacement (signal instability) |
| **Power-On Warmup** | 2-second gradual brightness increase on startup |

All CRT effects can be toggled with the **R** key.

## License

This project is licensed under the [MIT License](LICENSE).

Space Invaders is © Taito Corporation. This emulator is for educational purposes only.

## Acknowledgments

- Original Space Invaders by Tomohiro Nishikado (Taito, 1978)
- Intel 8080 documentation and reference materials
- SDL2 and SFML libraries
