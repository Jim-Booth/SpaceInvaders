// ============================================================================
// Project:     SpaceInvaders
// File:        SpaceInvadersEmulator.cs
// Description: Browser-compatible emulator wrapper with Canvas rendering
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using Microsoft.JSInterop;
using SpaceInvaders.MAINBOARD;

namespace SpaceInvaders
{
    public class SpaceInvadersEmulator
    {
        private readonly IJSRuntime _js;
        private readonly HttpClient _http;
        private Intel8080? _cpu;
        
        private const int ScreenWidth = 223;
        private const int ScreenHeight = 256;
        
        private readonly byte[] _colorLookup;
        private readonly byte[] _inputPorts = [0x0E, 0x08, 0x00, 0x00];
        
        private byte _prevPort3;
        private byte _prevPort5;
        
        // RGBA color values for Canvas ImageData (R, G, B, A byte order)
        // Green for player/shields area
        private const byte GreenR = 0x0F, GreenG = 0xDF, GreenB = 0x1F;
        // White for main play area
        private const byte WhiteR = 0xE8, WhiteG = 0xEC, WhiteB = 0xFF;
        // Red for UFO area
        private const byte RedR = 0xFF, RedG = 0x10, RedB = 0x50;
        
        public bool IsInitialized { get; private set; }
        
        public SpaceInvadersEmulator(IJSRuntime js, HttpClient http)
        {
            _js = js;
            _http = http;
            _colorLookup = BuildColorLookup();
        }
        
        // Color zone: 0 = White, 1 = Green, 2 = Red
        private static byte[] BuildColorLookup()
        {
            var lookup = new byte[256];
            for (int y = 0; y < 256; y++)
            {
                if (y > 195 && y < 242)
                    lookup[y] = 1;      // Green: Player and shields area
                else if (y > 240)
                    lookup[y] = 0;      // White: Bottom lives area (green handled separately for X < 127)
                else if (y > 32 && y < 64)
                    lookup[y] = 2;      // Red: UFO area
                else
                    lookup[y] = 0;      // White: Default play area
            }
            return lookup;
        }
        
        public async Task InitializeAsync()
        {
            // Initialize canvas
            await _js.InvokeVoidAsync("gameInterop.initialize", "gameCanvas", ScreenWidth, ScreenHeight);
            
            // Load sounds
            await LoadSoundsAsync();
            
            // Load ROMs
            await LoadRomsAsync();
            
            IsInitialized = true;
        }
        
        private async Task LoadSoundsAsync()
        {
            string[] sounds = ["shoot", "explosion", "invaderkilled", "ufo_lowpitch", 
                              "fastinvader1", "fastinvader2", "fastinvader3", "fastinvader4", "extendedPlay"];
            
            foreach (var sound in sounds)
            {
                await _js.InvokeVoidAsync("gameInterop.loadSound", sound, $"sounds/{sound}.wav");
            }
        }
        
        private async Task LoadRomsAsync()
        {
            _cpu = new Intel8080(new Memory(0x10000));
            
            var romH = await _http.GetByteArrayAsync("roms/invaders.h");
            var romG = await _http.GetByteArrayAsync("roms/invaders.g");
            var romF = await _http.GetByteArrayAsync("roms/invaders.f");
            var romE = await _http.GetByteArrayAsync("roms/invaders.e");
            
            _cpu.Memory.LoadFromBytes(romH, 0x0000, 0x800);
            _cpu.Memory.LoadFromBytes(romG, 0x0800, 0x800);
            _cpu.Memory.LoadFromBytes(romF, 0x1000, 0x800);
            _cpu.Memory.LoadFromBytes(romE, 0x1800, 0x800);
            
            _cpu.Running = true;
        }
        
        public async Task RunFrameAsync()
        {
            if (_cpu == null || !_cpu.Running) return;
            
            // Run one frame of CPU cycles
            _cpu.RunFrame();
            
            // Convert video memory to RGBA
            byte[] _rgbaBuffer = ConvertVideoToRgba();
            
            // Send to canvas
            await _js.InvokeVoidAsync("gameInterop.drawFrame", _rgbaBuffer);
            
            // Check sound triggers
            await CheckSoundsAsync();
            
            // Update input ports
            _cpu.PortIn = _inputPorts;
        }
        
        private byte[] ConvertVideoToRgba()
        {
            byte[] _rgbaBuffer = new byte[ScreenWidth * ScreenHeight * 4];
            
            if (_cpu == null) return _rgbaBuffer;
            
            ReadOnlySpan<byte> video = _cpu.Video.AsSpan();
            
            int ptr = 0;
            for (int col = 0; col < ScreenWidth; col++)
            {
                for (int byteRow = 0; byteRow < 32; byteRow++)
                {
                    byte value = video[ptr++];
                    if (value == 0) continue;
                    
                    for (int bit = 0; bit < 8; bit++)
                    {
                        if ((value & (1 << bit)) == 0) continue;
                        
                        int videoY = byteRow * 8 + bit;
                        int screenY = 255 - videoY;
                        if (screenY < 0 || screenY >= ScreenHeight) continue;
                        
                        // Color lookup
                        int colorY = ScreenHeight - (byteRow * 8);
                        if (colorY >= ScreenHeight) colorY = ScreenHeight - 1;
                        if (colorY < 0) colorY = 0;
                        
                        // Determine color zone (special case: lives area with X < 127 is green)
                        byte colorZone = (colorY > 240 && col < 127) ? (byte)1 : _colorLookup[colorY];
                        
                        // Write RGBA pixel based on color zone
                        int idx = (screenY * ScreenWidth + col) * 4;
                        switch (colorZone)
                        {
                            case 1: // Green
                                _rgbaBuffer[idx + 0] = GreenR;
                                _rgbaBuffer[idx + 1] = GreenG;
                                _rgbaBuffer[idx + 2] = GreenB;
                                break;
                            case 2: // Red
                                _rgbaBuffer[idx + 0] = RedR;
                                _rgbaBuffer[idx + 1] = RedG;
                                _rgbaBuffer[idx + 2] = RedB;
                                break;
                            default: // White
                                _rgbaBuffer[idx + 0] = WhiteR;
                                _rgbaBuffer[idx + 1] = WhiteG;
                                _rgbaBuffer[idx + 2] = WhiteB;
                                break;
                        }
                        _rgbaBuffer[idx + 3] = 255; // Alpha
                    }
                }
            }
            return _rgbaBuffer;
        }
        
        private async Task CheckSoundsAsync()
        {
            if (_cpu == null) return;
            
            byte port3 = _cpu.PortOut[3];
            byte port5 = _cpu.PortOut[5];
            
            // Port 3 sounds
            if (port3 != _prevPort3)
            {
                if (((port3 & 0x01) != 0) && ((port3 & 0x01) != (_prevPort3 & 0x01)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "ufo_lowpitch");
                if (((port3 & 0x02) != 0) && ((port3 & 0x02) != (_prevPort3 & 0x02)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "shoot");
                if (((port3 & 0x04) != 0) && ((port3 & 0x04) != (_prevPort3 & 0x04)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "explosion");
                if (((port3 & 0x08) != 0) && ((port3 & 0x08) != (_prevPort3 & 0x08)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "invaderkilled");
                if (((port3 & 0x10) != 0) && ((port3 & 0x10) != (_prevPort3 & 0x10)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "extendedPlay");
            }
            _prevPort3 = port3;
            
            // Port 5 sounds
            if (port5 != _prevPort5)
            {
                if (((port5 & 0x01) != 0) && ((port5 & 0x01) != (_prevPort5 & 0x01)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "fastinvader1");
                if (((port5 & 0x02) != 0) && ((port5 & 0x02) != (_prevPort5 & 0x02)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "fastinvader2");
                if (((port5 & 0x04) != 0) && ((port5 & 0x04) != (_prevPort5 & 0x04)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "fastinvader3");
                if (((port5 & 0x08) != 0) && ((port5 & 0x08) != (_prevPort5 & 0x08)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "fastinvader4");
                if (((port5 & 0x10) != 0) && ((port5 & 0x10) != (_prevPort5 & 0x10)))
                    await _js.InvokeVoidAsync("gameInterop.playSound", "explosion");
            }
            _prevPort5 = port5;
        }
        
        public void KeyDown(string key)
        {
            uint keyValue = MapKey(key);
            if (keyValue == 99) return;
            
            switch (keyValue)
            {
                case 1: _inputPorts[1] |= 0x01; break; // Coin
                case 2: _inputPorts[1] |= 0x04; break; // 1P Start
                case 3: _inputPorts[1] |= 0x02; break; // 2P Start
                case 4: _inputPorts[1] |= 0x20; _inputPorts[2] |= 0x20; break; // Left (both players)
                case 5: _inputPorts[1] |= 0x40; _inputPorts[2] |= 0x40; break; // Right (both players)
                case 6: _inputPorts[1] |= 0x10; _inputPorts[2] |= 0x10; break; // Fire (both players)
            }
        }
        
        public void KeyUp(string key)
        {
            uint keyValue = MapKey(key);
            if (keyValue == 99) return;
            
            switch (keyValue)
            {
                case 1: _inputPorts[1] &= 0xFE; break; // Coin
                case 2: _inputPorts[1] &= 0xFB; break; // 1P Start
                case 3: _inputPorts[1] &= 0xFD; break; // 2P Start
                case 4: _inputPorts[1] &= 0xDF; _inputPorts[2] &= 0xDF; break; // Left (both players)
                case 5: _inputPorts[1] &= 0xBF; _inputPorts[2] &= 0xBF; break; // Right (both players)
                case 6: _inputPorts[1] &= 0xEF; _inputPorts[2] &= 0xEF; break; // Fire (both players)
            }
        }
        
        private static uint MapKey(string key)
        {
            return key switch
            {
                "c" or "C" => 1,           // Coin
                "1" => 2,                   // 1P Start
                "2" => 3,                   // 2P Start
                "ArrowLeft" => 4,           // Left
                "ArrowRight" => 5,          // Right
                " " => 6,                   // Fire (Space)
                _ => 99
            };
        }
    }
}
