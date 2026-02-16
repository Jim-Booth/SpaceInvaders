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
        
        // Pre-allocated frame buffer to avoid ~223KB allocation per frame (~13MB/s GC pressure at 60fps)
        private readonly byte[] _rgbaBuffer = new byte[ScreenWidth * ScreenHeight * 4];
        
        // Snapshot of previous video memory for dirty-checking (skip render when unchanged)
        private readonly byte[] _prevVideo = new byte[ScreenWidth * 32];
        private bool _firstFrame = true;
        
        // Reusable list for batching sound triggers into a single JS interop call per frame
        private readonly List<string> _soundBatch = new(4);
        
        private byte _prevPort3;
        private byte _prevPort5;
        
        // RGBA color values for Canvas ImageData (R, G, B, A byte order)
        // Green for player/shields area
        private const byte GreenR = 0x0F, GreenG = 0xDF, GreenB = 0x1F;
        // White for main play area
        private const byte WhiteR = 0xE8, WhiteG = 0xEC, WhiteB = 0xFF;
        // Red for UFO area
        private const byte RedR = 0xFF, RedG = 0x10, RedB = 0x50;
        
        // Pre-computed RGBA color table indexed by color zone (0=White, 1=Green, 2=Red)
        // Each entry: [R, G, B, A] — eliminates per-pixel switch branching
        private static readonly byte[][] ColorTable =
        [
            [WhiteR, WhiteG, WhiteB, 255],
            [GreenR, GreenG, GreenB, 255],
            [RedR,   RedG,   RedB,   255]
        ];
        
        public bool IsInitialized { get; private set; }
        public List<string> MissingRoms { get; } = new();
        public List<string> MissingSounds { get; } = new();
        
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
        
        public async Task<bool> InitializeAsync()
        {
            // Initialize canvas
            await _js.InvokeVoidAsync("gameInterop.initialize", "gameCanvas", ScreenWidth, ScreenHeight);
            
            // Load sounds (non-critical)
            await LoadSoundsAsync();
            
            // Load ROMs (critical)
            await LoadRomsAsync();
            
            if (MissingRoms.Count > 0)
                return false;
            
            IsInitialized = true;
            return true;
        }
        
        private async Task LoadSoundsAsync()
        {
            string[] sounds = ["shoot", "explosion", "invaderkilled", "ufo_lowpitch", 
                              "fastinvader1", "fastinvader2", "fastinvader3", "fastinvader4", "extendedPlay"];
            
            foreach (var sound in sounds)
            {
                var path = $"sounds/{sound}.wav";
                try
                {
                    var response = await _http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        await _js.InvokeVoidAsync("gameInterop.loadSound", sound, path);
                    }
                    else
                    {
                        MissingSounds.Add($"{sound}.wav");
                    }
                }
                catch
                {
                    MissingSounds.Add($"{sound}.wav");
                }
            }
        }
        
        private async Task LoadRomsAsync()
        {
            _cpu = new Intel8080(new Memory(0x10000));
            
            var romFiles = new (string Name, int Address)[] 
            {
                ("invaders.h", 0x0000),
                ("invaders.g", 0x0800),
                ("invaders.f", 0x1000),
                ("invaders.e", 0x1800)
            };
            
            var romData = new Dictionary<string, byte[]>();
            
            foreach (var (name, _) in romFiles)
            {
                try
                {
                    var data = await _http.GetByteArrayAsync($"roms/{name}");
                    romData[name] = data;
                }
                catch
                {
                    MissingRoms.Add(name);
                }
            }
            
            if (MissingRoms.Count > 0)
                return;
            
            foreach (var (name, address) in romFiles)
            {
                _cpu.Memory.LoadFromBytes(romData[name], address, 0x800);
            }
            
            _cpu.Running = true;
        }
        
        public async Task RunFrameAsync()
        {
            if (_cpu == null || !_cpu.Running) return;
            
            // Run one frame of CPU cycles
            _cpu.RunFrame();
            
            // Only render and send frame if video memory has changed
            ReadOnlySpan<byte> video = _cpu.Video.AsSpan(0, ScreenWidth * 32);
            bool videoChanged = _firstFrame || !video.SequenceEqual(_prevVideo);
            
            if (videoChanged)
            {
                _firstFrame = false;
                video.CopyTo(_prevVideo);
                
                // Convert video memory to RGBA (reuses pre-allocated buffer)
                DrawRGBAVideoFrame(video);
                
                // Send frame to canvas
                await _js.InvokeVoidAsync("gameInterop.drawFrame", _rgbaBuffer);
            }
            
            // Batch sound triggers into a single JS interop call
            CollectSoundTriggers();
            if (_soundBatch.Count > 0)
            {
                await _js.InvokeVoidAsync("gameInterop.playSounds", _soundBatch);
                _soundBatch.Clear();
            }
            
            // Update input ports
            _cpu.PortIn = _inputPorts;
        }
        
        private void DrawRGBAVideoFrame(ReadOnlySpan<byte> video)
        {
            // Clear the pre-allocated buffer (all pixels to transparent black)
            Array.Clear(_rgbaBuffer);
            
            int ptr = 0;
            for (int col = 0; col < ScreenWidth; col++)
            {
                for (int byteRow = 0; byteRow < 32; byteRow++)
                {
                    byte value = video[ptr++];
                    if (value == 0) continue;
                    
                    // Hoist color lookup out of the bit loop — colorY is constant per byteRow
                    int colorY = ScreenHeight - (byteRow * 8);
                    if (colorY >= ScreenHeight) colorY = ScreenHeight - 1;
                    else if (colorY < 0) colorY = 0;
                    
                    // Determine color zone (special case: lives area with X < 127 is green)
                    byte colorZone = (colorY > 240 && col < 127) ? (byte)1 : _colorLookup[colorY];
                    byte[] rgba = ColorTable[colorZone];
                    
                    for (int bit = 0; bit < 8; bit++)
                    {
                        if ((value & (1 << bit)) == 0) continue;
                        
                        int screenY = 255 - (byteRow * 8 + bit);
                        if (screenY < 0 || screenY >= ScreenHeight) continue;
                        
                        // Write RGBA pixel from pre-computed color table
                        int idx = (screenY * ScreenWidth + col) * 4;
                        _rgbaBuffer[idx]     = rgba[0];
                        _rgbaBuffer[idx + 1] = rgba[1];
                        _rgbaBuffer[idx + 2] = rgba[2];
                        _rgbaBuffer[idx + 3] = rgba[3];
                    }
                }
            }
        }
        
        private void CollectSoundTriggers()
        {
            if (_cpu == null) return;
            
            byte port3 = _cpu.PortOut[3];
            byte port5 = _cpu.PortOut[5];
            
            // Port 3 sounds — collect triggered sound IDs
            if (port3 != _prevPort3)
            {
                byte rising3 = (byte)(port3 & ~_prevPort3); // bits that transitioned 0?1
                if ((rising3 & 0x01) != 0) _soundBatch.Add("ufo_lowpitch");
                if ((rising3 & 0x02) != 0) _soundBatch.Add("shoot");
                if ((rising3 & 0x04) != 0) _soundBatch.Add("explosion");
                if ((rising3 & 0x08) != 0) _soundBatch.Add("invaderkilled");
                if ((rising3 & 0x10) != 0) _soundBatch.Add("extendedPlay");
            }
            _prevPort3 = port3;
            
            // Port 5 sounds — collect triggered sound IDs
            if (port5 != _prevPort5)
            {
                byte rising5 = (byte)(port5 & ~_prevPort5); // bits that transitioned 0?1
                if ((rising5 & 0x01) != 0) _soundBatch.Add("fastinvader1");
                if ((rising5 & 0x02) != 0) _soundBatch.Add("fastinvader2");
                if ((rising5 & 0x04) != 0) _soundBatch.Add("fastinvader3");
                if ((rising5 & 0x08) != 0) _soundBatch.Add("fastinvader4");
                if ((rising5 & 0x10) != 0) _soundBatch.Add("explosion");
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
