# Space Invaders - Intel 8080 Emulator

A cross-platform Intel 8080 Space Invaders arcade emulator built with .NET 9 and Blazor WebAssembly.

![Space Invaders](https://img.shields.io/badge/.NET-9.0-512BD4) ![Platform](https://img.shields.io/badge/Platform-Web%20Browser-lightgrey)

## Features

- **Accurate Intel 8080 CPU emulation** - Full implementation of all 8080 opcodes
- **Authentic display rendering** - Color zones matching original arcade cabinet (green, red, white)
- **Browser-based** - Runs entirely in the browser using WebAssembly
- **Authentic audio** - Sound effects via Web Audio API with correct UFO looping behaviour
- **Cross-platform** - Runs on any modern browser (Chrome, Firefox, Safari, Edge)
- **Mobile support** - On-screen touch controls for phones and tablets
- **Pause** - Press **P** to pause and resume at any time
- **Persistent high score** - High score is saved in browser `localStorage` and restored on next visit

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Build and Run

```bash
dotnet build
dotnet run
```

Then open your browser to `https://localhost:5443` or `http://localhost:5000`.

## Controls

### Desktop (Keyboard)

| Key | Action |
|-----|--------|
| **C** | Insert Coin |
| **1** | 1 Player Start |
| **2** | 2 Player Start |
| **←** / **→** | Move |
| **Space** | Fire |
| **P** | Pause / Resume |

### Mobile (Touch Controls)

On mobile devices a set of on-screen touch buttons is displayed below the game screen, replacing the keyboard hints shown on desktop. The buttons are arranged in two rows that match the width of the game canvas:

| Row | Buttons |
|-----|---------|
| **Top** | **1P** (1 player start) · **COIN** (insert coin) · **2P** (2 player start) |
| **Bottom** | **◀** (move left) · **FIRE** · **▶** (move right) |

Mobile detection is automatic — touch controls appear only on phones and tablets, while desktop users continue to use the keyboard as normal.

## ROM Files

Place the following ROM files in the `wwwroot/roms/` directory:

| File | Address Range | MD5 Checksum |
|------|---------------|--------------|
| `invaders.h` | 0x0000 - 0x07FF | `E87815985F5208BFA25D567C3FB52418` |
| `invaders.g` | 0x0800 - 0x0FFF | `9EC2DC89315A0D50C5E166F664F64A48` |
| `invaders.f` | 0x1000 - 0x17FF | `7709A2576ADB6FEDCDFE175759E5C17A` |
| `invaders.e` | 0x1800 - 0x1FFF | `7D3B201F3E84AF3B4FCB8CE8619EC9C6` |

## Sound Files

Place WAV sound files in the `wwwroot/sounds/` directory:

- `ufo_lowpitch.wav`
- `shoot.wav`
- `explosion.wav`
- `invaderkilled.wav`
- `fastinvader1.wav`
- `fastinvader2.wav`
- `fastinvader3.wav`
- `fastinvader4.wav`
- `extendedPlay.wav`

## Project Structure

```
SpaceInvaders/
├── Program.cs                  # Blazor WASM entry point
├── App.razor                   # Root Blazor component
├── _Imports.razor              # Global Razor imports
├── SpaceInvadersEmulator.cs    # Emulator wrapper with Canvas rendering
├── Pages/
│   └── Index.razor             # Main game page
├── MAINBOARD/
│   ├── Intel8080.cs            # CPU emulation core
│   ├── Memory.cs               # 64KB addressable memory
│   ├── Registers.cs            # CPU registers
│   └── Flags.cs                # Status flags (Z, S, P, CY, AC)
└── wwwroot/
    ├── index.html              # HTML host page
    ├── css/app.css             # Styles
    ├── js/game.js              # Canvas and audio interop
    ├── roms/                   # ROM files
    └── sounds/                 # Sound effect WAV files
```

## Technical Details

- **CPU Clock**: 2 MHz emulated
- **Display**: 224x256 (rotated 90° from original 256x224)
- **Refresh Rate**: 60 Hz
- **Color Overlay**: Simulates original arcade color gel overlay
  - Green: Player and shields area
  - Red: UFO area at top
  - White: Middle play area

### Rendering Architecture

Game logic and rendering run on separate timers to avoid tearing and correctly handle high-refresh-rate displays:

- **Game logic** — driven by a C# `PeriodicTimer` at 60 Hz. Each tick runs one full frame of 8080 CPU cycles, collects sound triggers, and writes the pixel buffer to a JS `ImageData` object via `updateFrame()`.
- **Rendering** — driven by a JS `requestAnimationFrame` loop. Each VSync it flushes the latest `ImageData` to the canvas via `putImageData()`. This keeps drawing aligned with the display's paint cycle regardless of whether the display runs at 60, 120, or 144 Hz.

### Audio

All sounds use the Web Audio API (`AudioContext` + `AudioBuffer`). Sounds are decoded once at load time and played by creating a new `AudioBufferSourceNode` per trigger — giving low latency and correct overlap without DOM node leaks. The UFO hum (`ufo_lowpitch`) is treated as a sustained level signal: it loops continuously while port 3 bit 0 is high and stops when the bit falls, matching the original hardware behaviour.

### High Score Persistence

The high score is persisted in browser `localStorage` under the key `spaceInvadersHighScore`. On startup the stored value is decoded and written directly into the ROM's BCD RAM (`0x20F4–0x20F5`) so the game displays it natively from the first frame. It is saved once per game, triggered by detecting the ROM's `gameMode` flag (`0x20EF`) transitioning from `1` to `0` — the ROM's own authoritative end-of-game signal.

## License

This project is licensed under the [MIT License](LICENSE).

Space Invaders is © Taito Corporation. This emulator is for educational purposes only.

## Acknowledgments

- Original Space Invaders by Tomohiro Nishikado (Taito, 1978)
- Intel 8080 documentation and reference materials
- Blazor WebAssembly
