# Space Invaders - Intel 8080 Emulator - Web

A cross-platform Intel 8080 Space Invaders arcade emulator built with .NET 9 and Blazor WebAssembly.

![Space Invaders](https://img.shields.io/badge/.NET-9.0-512BD4) ![Platform](https://img.shields.io/badge/Platform-Web%20Browser-lightgrey)

## Features

- **Accurate Intel 8080 CPU emulation** - Full implementation of all 8080 opcodes
- **Authentic display rendering** - Color zones matching original arcade cabinet (green, red, white)
- **Browser-based** - Runs entirely in the browser using WebAssembly
- **Authentic audio** - Sound effects via Web Audio API
- **Cross-platform** - Runs on any modern browser (Chrome, Firefox, Safari, Edge)

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Build and Run

```bash
dotnet build
dotnet run
```

Then open your browser to `https://localhost:5001` or `http://localhost:5000`

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

## License

This project is licensed under the [MIT License](LICENSE).

Space Invaders is © Taito Corporation. This emulator is for educational purposes only.

## Acknowledgments

- Original Space Invaders by Tomohiro Nishikado (Taito, 1978)
- Intel 8080 documentation and reference materials
- Blazor WebAssembly
