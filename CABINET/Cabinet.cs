// ============================================================================
// Project:     SpaceInvaders
// File:        Cabinet.cs
// Description: Arcade cabinet simulation with SDL2 rendering, input handling,
//              display threading, and audio playback
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using SpaceInvaders.MAINBOARD;
using SDL2;

namespace SpaceInvaders.CABINET
{
    public class Cabinet
    {
        private Intel8080? _cpu;
        private Thread? _portThread;
        private Thread? _cpuThread;
        private Thread? _displayThread;
        private Thread? _soundThread;

        private static readonly CancellationTokenSource _cancellationTokenSource = new();
        private static readonly CancellationToken _displayLoop = _cancellationTokenSource.Token;
        private static readonly CancellationToken _soundLoop = _cancellationTokenSource.Token;
        private static readonly CancellationToken _portLoop = _cancellationTokenSource.Token;

        private readonly byte[] _inputPorts = [0x0E, 0x08, 0x00, 0x00];
        private readonly GameSettings _settings;
        private readonly int ScreenWidth = 223;
        private readonly int ScreenHeight = 256;
        private int TitleBarHeight; // Calculated based on screen multiplier
        private volatile int _screenMultiplier = 3;
        private readonly object _resizeLock = new();
        private volatile bool _resizePending = false;
        private volatile int _pendingMultiplier = 2;
        private volatile bool _frameReady = false;
        private uint[]? _renderBuffer;
        
        // CRT effects handler
        private CrtEffects? _crtEffects;
        
        // Overlay renderer
        private OverlayRenderer? _overlay;
        
        private IntPtr _window;
        private IntPtr _renderer;
        private IntPtr _texture;
        private IntPtr _backgroundTexture;
        private bool _backgroundEnabled = true;
        private bool _soundEnabled = true;
        private bool _gamePaused = false;
        private uint[] _pixelBuffer;
        private static readonly string AppPath = AppDomain.CurrentDomain.BaseDirectory;

        private readonly CachedSound _ufoLowpitch = new(Path.Combine(AppPath, "SOUNDS", "ufo_lowpitch.wav"));
        private readonly CachedSound _shoot = new(Path.Combine(AppPath, "SOUNDS", "shoot.wav"));
        private readonly CachedSound _invaderkilled = new(Path.Combine(AppPath, "SOUNDS", "invaderkilled.wav"));
        private readonly CachedSound _fastinvader1 = new(Path.Combine(AppPath, "SOUNDS", "fastinvader1.wav"));
        private readonly CachedSound _fastinvader2 = new(Path.Combine(AppPath, "SOUNDS", "fastinvader2.wav"));
        private readonly CachedSound _fastinvader3 = new(Path.Combine(AppPath, "SOUNDS", "fastinvader3.wav"));
        private readonly CachedSound _fastinvader4 = new(Path.Combine(AppPath, "SOUNDS", "fastinvader4.wav"));
        private readonly CachedSound _explosion = new(Path.Combine(AppPath, "SOUNDS", "explosion.wav"));
        private readonly CachedSound _extendedplay = new(Path.Combine(AppPath, "SOUNDS", "extendedPlay.wav"));

        // Pre-computed ARGB color values for each color zone
        private const uint ColorGreen = 0xC00FDF0F;   // ARGB: alpha=0xC0, R=0x0F, G=0xDF, B=0x0F
        private const uint ColorWhite = 0xC0EFEFFF;   // ARGB: alpha=0xC0, R=0xEF, G=0xEF, B=0xFF
        private const uint ColorWhite2 = 0xF0EFEFFF;  // ARGB: alpha=0xF0, R=0xEF, G=0xEF, B=0xFF
        private const uint ColorRed = 0xC0FF0040;     // ARGB: alpha=0xC0, R=0xFF, G=0x00, B=0x40
        
        // Pre-computed color lookup table indexed by unscaled Y coordinate (0-255)
        // This eliminates per-pixel color zone calculations
        private uint[] _colorLookup = null!;

        public Cabinet()
        {
            _settings = GameSettings.Load();
            ApplyDipSwitches();
            TitleBarHeight = OverlayRenderer.GetTitleBarHeight(_screenMultiplier);
            _pixelBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
            _renderBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
            BuildColorLookupTable();
            InitializeSDL();
            LoadBackgroundTexture();
            _crtEffects = new CrtEffects(_renderer, ScreenWidth, ScreenHeight, _screenMultiplier);
            _overlay = new OverlayRenderer(_renderer);
        }
        
        /// <summary>
        /// Builds a pre-computed color lookup table for each Y coordinate.
        /// This eliminates per-pixel color zone boundary checks during rendering.
        /// 
        /// Based on original GetColorValue method (unscaled screen coordinates, Y=0 at top):
        /// - Green: Y > 195 && Y < 239 (player and shields area)
        /// - White2: Y > 240 && Y < 256 (bottom lives area - green override for X < 127)
        /// - Red: Y > 32 && Y < 64 (UFO area)
        /// - White: everything else
        /// </summary>
        private void BuildColorLookupTable()
        {
            _colorLookup = new uint[ScreenHeight];
            for (int y = 0; y < ScreenHeight; y++)
            {
                // Color zones matching original GetColorValue logic (unscaled coordinates)
                if (y > 195 && y < 239)
                    _colorLookup[y] = ColorGreen;      // Player and shields area
                else if (y > 240)
                    _colorLookup[y] = ColorWhite2;     // Bottom lives area (green handled separately for X < 127)
                else if (y > 32 && y < 64)
                    _colorLookup[y] = ColorRed;        // UFO area
                else
                    _colorLookup[y] = ColorWhite;      // Default play area
            }
        }

        /// <summary>
        /// Loads the cabinet background texture from Cabinet.bmp if available.
        /// </summary>
        private void LoadBackgroundTexture()
        {
            string backgroundPath = Path.Combine(AppPath, "Cabinet.bmp");
            if (!File.Exists(backgroundPath))
            {
                return;
            }

            IntPtr surface = SDL.SDL_LoadBMP(backgroundPath);
            
            if (surface == IntPtr.Zero)
            {
                return;
            }

            _backgroundTexture = SDL.SDL_CreateTextureFromSurface(_renderer, surface);
            SDL.SDL_FreeSurface(surface);

            if (_backgroundTexture != IntPtr.Zero)
            {
                // Ensure background renders without blending (fully opaque)
                SDL.SDL_SetTextureBlendMode(_backgroundTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_NONE);
            }
        }

        /// <summary>
        /// Requests a display resize to a new scale multiplier (1-4x).
        /// </summary>
        private void RequestResize(int newMultiplier)
        {
            if (newMultiplier < 1 || newMultiplier > 4 || newMultiplier == _screenMultiplier)
                return;

            _pendingMultiplier = newMultiplier;
            _resizePending = true;
        }

        /// <summary>
        /// Processes any pending resize request on the main thread.
        /// </summary>
        private void ProcessPendingResize()
        {
            if (!_resizePending)
                return;

            int newMultiplier = _pendingMultiplier;

            if (newMultiplier < 1 || newMultiplier > 4 || newMultiplier == _screenMultiplier)
            {
                _resizePending = false;
                return;
            }

            // Wait briefly for display thread to notice the pending flag and yield
            Thread.Sleep(50);

            // Use TryEnter with timeout to avoid deadlock
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_resizeLock, 100, ref lockTaken);
                if (!lockTaken)
                {
                    // Could not acquire lock, will retry next frame
                    return;
                }

                _screenMultiplier = newMultiplier;
                _resizePending = false;
                
                // Destroy old texture
                if (_texture != IntPtr.Zero)
                    SDL.SDL_DestroyTexture(_texture);
                
                // Recreate pixel buffer at new scaled size
                _pixelBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
                _renderBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
                _frameReady = false;
                
                // Color lookup table doesn't need rebuilding - it's based on unscaled coordinates
                
                // Recreate texture at new scaled size
                _texture = SDL.SDL_CreateTexture(
                    _renderer,
                    SDL.SDL_PIXELFORMAT_ARGB8888,
                    (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                    ScreenWidth * _screenMultiplier,
                    ScreenHeight * _screenMultiplier
                );
                
                if (_texture == IntPtr.Zero)
                {
                    throw new Exception($"Texture could not be created! SDL_Error: {SDL.SDL_GetError()}");
                }
                
                // Enable alpha blending on the game texture so background shows through
                SDL.SDL_SetTextureBlendMode(_texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                
                // Resize CRT effects textures for new size
                _crtEffects?.Resize(ScreenWidth, ScreenHeight, _screenMultiplier);
                
                // Resize window and re-center (include title bar height)
                int newTitleBarHeight = OverlayRenderer.GetTitleBarHeight(_screenMultiplier);
                SDL.SDL_SetWindowSize(_window, ScreenWidth * _screenMultiplier, ScreenHeight * _screenMultiplier + newTitleBarHeight);
                SDL.SDL_SetWindowPosition(_window, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED);
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_resizeLock);
            }
        }

        /// <summary>
        /// Initializes SDL2 video subsystem, window, renderer, and texture.
        /// </summary>
        private void InitializeSDL()
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                throw new Exception($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");
            }

            _window = SDL.SDL_CreateWindow(
                "Space Invaders",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                ScreenWidth * _screenMultiplier,
                ScreenHeight * _screenMultiplier + TitleBarHeight,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS
            );

            if (_window == IntPtr.Zero)
            {
                throw new Exception($"Window could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }

            // Enable linear filtering for smooth CRT-like appearance
            SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1");

            _renderer = SDL.SDL_CreateRenderer(_window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (_renderer == IntPtr.Zero)
            {
                throw new Exception($"Renderer could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }

            // Create streaming texture at scaled resolution for pixel-perfect rendering
            _texture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                ScreenWidth * _screenMultiplier,
                ScreenHeight * _screenMultiplier
            );
            
            if (_texture == IntPtr.Zero)
            {
                throw new Exception($"Texture could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }
            
            // Enable alpha blending on the game texture so background shows through
            SDL.SDL_SetTextureBlendMode(_texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        }

        /// <summary>
        /// Applies DIP switch settings to Port 2.
        /// Preserves player input bits (4-6) while setting DIP bits (0-1, 3, 7).
        /// </summary>
        private void ApplyDipSwitches()
        {
            byte dipBits = _settings.GetPort2DipBits();
            // Clear DIP switch bits (0-1, 3, 7) and preserve player input bits (2, 4-6)
            _inputPorts[2] = (byte)((_inputPorts[2] & 0x74) | dipBits);
        }

        /// <summary>
        /// Starts the emulator and runs the main event loop until exit.
        /// </summary>
        public void PowerOn()
        {
            ExecuteSpaceInvaders();
            
            // Monitor for SDL events
            SDL.SDL_Event sdlEvent;
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Process any pending resize on the main thread (required for SDL on Windows)
                ProcessPendingResize();
                
                // Process SDL events
                while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                {
                    if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        Console.WriteLine("Exiting...");
                        _cancellationTokenSource.Cancel();
                        _cpu?.Stop();
                        break;
                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
                    {
                        // Check if close button was clicked
                        if (sdlEvent.button.button == SDL.SDL_BUTTON_LEFT && _overlay != null)
                        {
                            if (_overlay.IsPointInCloseButton(sdlEvent.button.x, sdlEvent.button.y))
                            {
                                Console.WriteLine("Exiting...");
                                _cancellationTokenSource.Cancel();
                                _cpu?.Stop();
                                break;
                            }
                        }
                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN)
                    {
                        HandleKeyDown(sdlEvent.key.keysym.sym);
                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYUP)
                    {
                        HandleKeyUp(sdlEvent.key.keysym.sym);
                    }
                }
                
                // Render frame on main thread (required for SDL on Windows)
                RenderFrame();
                
                try
                {
                    Task.Delay(16, _cancellationTokenSource.Token).Wait(); // ~60 FPS event polling
                }
                catch (AggregateException) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Expected when cancellation is triggered
                }
            }
            
            // Save high score before shutdown (only if changed)
            if (_cpu != null)
            {
                int currentHighScore = _cpu.Memory.ReadHighScore();
                if (currentHighScore > _settings.HighScore)
                {
                    _settings.HighScore = currentHighScore;
                    _settings.Save();
                }
            }          
            
            // Wait for threads to finish
            Console.Write("Waiting for threads to terminate.");
            _cpuThread?.Join(2000);
            Console.Write(".");
            _portThread?.Join(1000);
            Console.Write(".");
            _displayThread?.Join(1000);
            Console.Write(".");
            _soundThread?.Join(1000);
            Console.Write(". ");

            // Play CRT power-off animation (only if CRT effects are enabled)
            if (_crtEffects?.Enabled == true)
            {
                int scaledWidth = ScreenWidth * _screenMultiplier;
                int scaledHeight = ScreenHeight * _screenMultiplier;
                _crtEffects.RenderPowerOffAnimation(_renderer, _texture, scaledWidth, scaledHeight);
            }

            AudioPlaybackEngine.Instance.Dispose();
            
            // Cleanup SDL
            _crtEffects?.Dispose();
            if (_texture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_texture);
            if (_backgroundTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_backgroundTexture);
            if (_renderer != IntPtr.Zero)
                SDL.SDL_DestroyRenderer(_renderer);
            if (_window != IntPtr.Zero)
                SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Quit();
            
            Console.Write("Done.");
        }

        /// <summary>
        /// Initializes and starts the Intel 8080 CPU and all background threads.
        /// </summary>
        private void ExecuteSpaceInvaders()
        {
            _cpu = new Intel8080(new Memory(0x10000));
            _cpu.Memory.LoadFromFile(Path.Combine(AppPath, "ROMS", "invaders.h"), 0x0000, 0x800); // invaders.h 0000 - 07FF
            _cpu.Memory.LoadFromFile(Path.Combine(AppPath, "ROMS", "invaders.g"), 0x0800, 0x800); // invaders.g 0800 - 0FFF
            _cpu.Memory.LoadFromFile(Path.Combine(AppPath, "ROMS", "invaders.f"), 0x1000, 0x800); // invaders.f 1000 - 17FF
            _cpu.Memory.LoadFromFile(Path.Combine(AppPath, "ROMS", "invaders.e"), 0x1800, 0x800); // invaders.e 1800 - 1FFF

            _cpuThread = new Thread(async () => 
            {
                try
                {
                    await _cpu!.StartAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                }
            })
            {
                Priority = ThreadPriority.Highest
            };
            _cpuThread.Start();

            while (!_cpu.Running) { }
            
            // Restore high score from persistent settings
            // Must be done AFTER CPU starts, as the game's init code clears RAM
            if (_settings.HighScore > 0)
            {
                // Wait for game initialization to complete (clears RAM including high score area)
                Thread.Sleep(100);
                _cpu.Memory.WriteHighScore(_settings.HighScore);
            }

            _portThread = new Thread(PortThread)
            {
                IsBackground = true
            };
            _portThread.Start();

            _displayThread = new Thread(DisplayThread)
            {
                IsBackground = true
            };
            _displayThread.Start();

            _soundThread = new Thread(SoundThread)
            {
                IsBackground = true
            };
            _soundThread.Start();
        }

        /// <summary>
        /// Background thread that synchronizes input port data with the CPU.
        /// </summary>
        private async void PortThread()
        {
            while (!_portLoop.IsCancellationRequested)
            {
                while (_cpu!.PortIn == _inputPorts)
                {
                    try
                    {
                        await Task.Delay(4, _portLoop);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                _cpu.PortIn = _inputPorts;
            }
        }

        /// <summary>
        /// Background thread that prepares frame data from video memory.
        /// Optimized with pre-computed color lookup and Span-based operations.
        /// </summary>
        public void DisplayThread()
        {
            while (!_displayLoop.IsCancellationRequested)
            {
                // Use timeout so we can still prepare frames when paused
                bool signaled = _cpu!.DisplayTiming.WaitOne(16);
                
                // Skip if resize is pending
                if (_resizePending)
                {
                    Thread.Sleep(1);
                    continue;
                }
                
                // If paused, just mark last frame as ready for re-render
                if (_gamePaused && !signaled)
                {
                    _frameReady = true;
                    continue;
                }
                
                // Prepare the frame data (no SDL calls here)
                lock (_resizeLock)
                {
                    if (_resizePending) continue;
                    
                    try
                    {
                        int multiplier = _screenMultiplier;
                        int scaledWidth = ScreenWidth * multiplier;
                        int scaledHeight = ScreenHeight * multiplier;
                        
                        // Use Span for faster array access
                        Span<uint> pixelSpan = _pixelBuffer.AsSpan();
                        ReadOnlySpan<byte> videoSpan = _cpu.Video.AsSpan();
                        ReadOnlySpan<uint> colorLookup = _colorLookup.AsSpan();
                        
                        // Apply phosphor persistence (fade previous frame) or clear
                        if (_crtEffects != null && _crtEffects.Enabled)
                        {
                            _crtEffects.ApplyPersistence(_pixelBuffer);
                        }
                        else
                        {
                            // Clear pixel buffer using Span.Clear (faster than Array.Clear)
                            pixelSpan.Clear();
                        }

                        int ptr = 0;
                        
                        // Process video memory column by column
                        // Original display is 256x224, rotated 90° CCW to 224x256
                        // Video memory is organized as columns of 8 pixels per byte
                        // Byte 0 contains Y pixels 0-7 (bottom of screen), bit 0 = Y0
                        for (int col = 0; col < ScreenWidth; col++)
                        {
                            int scaledX = col * multiplier;
                            
                            // Each column has 32 bytes (256 pixels / 8 bits per byte)
                            for (int byteRow = 0; byteRow < 32; byteRow++)
                            {
                                byte value = videoSpan[ptr++];
                                if (value == 0) continue; // Skip empty bytes (common case)
                                
                                // Process 8 bits in this byte
                                // Bit 0 is lowest Y in this group, bit 7 is highest
                                for (int bit = 0; bit < 8; bit++)
                                {
                                    if ((value & (1 << bit)) == 0) continue;
                                    
                                    // Calculate unscaled Y coordinate for pixel placement
                                    // Video memory byte 0 = bottom of screen (Y=255 in screen coords)
                                    // Screen Y=0 is TOP, Y=255 is BOTTOM
                                    int videoY = byteRow * 8 + bit;
                                    int screenY = (ScreenHeight - 1) - videoY;
                                    if (screenY < 0 || screenY >= ScreenHeight) continue;
                                    
                                    // For color lookup, use the byte row's base Y position
                                    // This matches original GetColorValue which used 'y' (the loop variable),
                                    // not the individual pixel Y. The original 'y' was:
                                    // y = scaledHeight - (byteRow * 8 * multiplier), then unscaled = y / multiplier
                                    // Which simplifies to: colorY = screenHeight - (byteRow * 8) = (255 - byteRow*8)
                                    int colorY = (ScreenHeight) - (byteRow * 8);
                                    if (colorY >= ScreenHeight) colorY = ScreenHeight - 1;
                                    if (colorY < 0) colorY = 0;
                                    
                                    // Look up color from pre-computed table
                                    // Handle special case: lives area (colorY > 240) with X < 127 is green
                                    uint colorValue;
                                    if (colorY > 240 && col < 127)
                                        colorValue = ColorGreen;
                                    else
                                        colorValue = colorLookup[colorY];
                                    
                                    // Calculate scaled pixel position
                                    int scaledY = screenY * multiplier;
                                    
                                    // Write scaled pixel block using optimized row fills
                                    for (int dy = 0; dy < multiplier; dy++)
                                    {
                                        int rowStart = (scaledY + dy) * scaledWidth + scaledX;
                                        if (rowStart >= 0 && rowStart + multiplier <= pixelSpan.Length)
                                        {
                                            // Use Span.Fill for each row of the scaled pixel
                                            pixelSpan.Slice(rowStart, multiplier).Fill(colorValue);
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Store current frame for next frame's persistence effect
                        if (_crtEffects != null && _crtEffects.Enabled)
                        {
                            _crtEffects.StorePersistence(_pixelBuffer);
                        }
                        
                        // Apply CRT post-processing effects
                        if (_crtEffects != null && _crtEffects.Enabled)
                        {
                            _crtEffects.ApplyPostProcessing(_pixelBuffer, scaledWidth, scaledHeight);
                        }

                        // Copy to render buffer for main thread to use
                        if (_renderBuffer != null && _renderBuffer.Length == _pixelBuffer.Length)
                        {
                            // Use Span-based copy (faster than Array.Copy for this size)
                            pixelSpan.CopyTo(_renderBuffer.AsSpan());
                            _frameReady = true;
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Renders the prepared frame to the screen. Must be called from the main thread.
        /// </summary>
        private void RenderFrame()
        {
            if (_resizePending || _renderBuffer == null)
                return;
            
            int scaledWidth = ScreenWidth * _screenMultiplier;
            int scaledHeight = ScreenHeight * _screenMultiplier;
            int titleBarHeight = OverlayRenderer.GetTitleBarHeight(_screenMultiplier);
            
            // Update texture with pixel buffer
            if (_frameReady)
            {
                lock (_resizeLock)
                {
                    if (_renderBuffer.Length == scaledWidth * scaledHeight)
                    {
                        unsafe
                        {
                            fixed (uint* pixels = _renderBuffer)
                            {
                                SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = scaledWidth, h = scaledHeight };
                                SDL.SDL_UpdateTexture(_texture, ref fullRect, (IntPtr)pixels, scaledWidth * sizeof(uint));
                            }
                        }
                    }
                }
            }

            // Clear renderer to black first
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL.SDL_RenderClear(_renderer);
            
            // Draw custom title bar
            _overlay?.DrawTitleBar(scaledWidth, _screenMultiplier, " space invaders - h for help");
            
            // Create destination rect offset by title bar height
            SDL.SDL_Rect gameDestRect = new SDL.SDL_Rect 
            { 
                x = 0, 
                y = titleBarHeight, 
                w = scaledWidth, 
                h = scaledHeight 
            };
            
            // Render background texture if enabled (offset by title bar)
            if (_backgroundEnabled && _backgroundTexture != IntPtr.Zero)
            {
                SDL.SDL_RenderCopy(_renderer, _backgroundTexture, IntPtr.Zero, ref gameDestRect);
            }
            
            // Get screen bounce offset for CRT power-on effect
            // Original CRT is rotated 90°, so deflection coil bounce is horizontal
            int bounceOffset = _crtEffects?.GetScreenBounceOffset(_screenMultiplier) ?? 0;
            
            // Render game texture on top (with alpha blending - transparent pixels show background)
            // Apply horizontal bounce offset during CRT warmup/settle
            SDL.SDL_Rect textureDestRect = new SDL.SDL_Rect 
            { 
                x = bounceOffset, 
                y = titleBarHeight, 
                w = scaledWidth, 
                h = scaledHeight 
            };
            SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, ref textureDestRect);
            
            if (_crtEffects != null && _crtEffects.Enabled)
            {
                // Set viewport to offset CRT overlays by title bar height
                SDL.SDL_Rect viewport = new SDL.SDL_Rect { x = 0, y = titleBarHeight, w = scaledWidth, h = scaledHeight };
                SDL.SDL_RenderSetViewport(_renderer, ref viewport);
                _crtEffects.RenderOverlays(_renderer, scaledWidth, scaledHeight, _screenMultiplier);
                
                // Reset viewport to full window
                SDL.SDL_Rect fullViewport = new SDL.SDL_Rect { x = 0, y = 0, w = scaledWidth, h = scaledHeight + titleBarHeight };
                SDL.SDL_RenderSetViewport(_renderer, ref fullViewport);
            }
            
            // Draw overlay message if active (offset by title bar)
            _overlay?.DrawMessage(scaledWidth, scaledHeight + titleBarHeight, _screenMultiplier);
            
            // Update and draw FPS counter, check for low FPS warning
            _overlay?.UpdateFps();
            if (_overlay != null && _overlay.FpsWarningEnabled && _crtEffects != null && _crtEffects.Enabled)
            {
                // Draw warning continuously while FPS is below 40 and CRT effects are enabled
                // Suppress warning during CRT warmup period (brightness fade-in)
                if (_overlay.CurrentFps > 0 && _overlay.CurrentFps < 40 && _crtEffects.WarmupComplete)
                {
                    _overlay.DrawLowFpsWarning(scaledWidth, _screenMultiplier, titleBarHeight);
                }
            }
            _overlay?.DrawFpsCounter(scaledWidth, _screenMultiplier, titleBarHeight);
            
            // Draw DIP switch and display settings overlay if enabled
            bool crtEnabled = _crtEffects?.Enabled ?? false;
            _overlay?.DrawDipSwitchOverlay(scaledWidth, scaledHeight + titleBarHeight, _screenMultiplier, _settings, 
                crtEnabled, _soundEnabled, _backgroundEnabled);
            
            // Draw controls help overlay if enabled
            _overlay?.DrawControlsOverlay(scaledWidth, scaledHeight + titleBarHeight, _screenMultiplier);
            
            SDL.SDL_RenderPresent(_renderer);
        }

        // GetColorValue method removed - replaced by pre-computed _colorLookup table
        // Color lookup is now O(1) array access instead of multiple conditional branches

        /// <summary>
        /// Handles keyboard key press events for game controls and settings.
        /// </summary>
        private void HandleKeyDown(SDL.SDL_Keycode key)
        {
            if (key == SDL.SDL_Keycode.SDLK_ESCAPE)
            {
                Console.WriteLine("Exiting...");
                _cancellationTokenSource.Cancel();
                _cpu?.Stop();
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_LEFTBRACKET)
            {
                RequestResize(_screenMultiplier - 1);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_RIGHTBRACKET)
            {
                RequestResize(_screenMultiplier + 1);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_b)
            {
                _backgroundEnabled = !_backgroundEnabled;
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_r)
            {
                if (_crtEffects != null)
                {
                    _crtEffects.Enabled = !_crtEffects.Enabled;
                    if (!_crtEffects.Enabled)
                    {
                        // Clear persistence buffer when disabling
                        _crtEffects.ClearPersistence();
                    }
                    _overlay?.ShowMessage(_crtEffects.Enabled ? "crt:on" : "crt:off", TimeSpan.FromSeconds(2));
                }
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_p)
            {
                _gamePaused = !_gamePaused;
                if (_cpu != null) _cpu.Paused = _gamePaused;
                if (_gamePaused)
                    _overlay?.ShowPersistentMessage("paused");
                else
                    _overlay?.ClearMessage();
                return;
            }
              
            if (key == SDL.SDL_Keycode.SDLK_s)
            {
                _soundEnabled = !_soundEnabled;
                _overlay?.ShowMessage(_soundEnabled ? "sound:on" : "sound:off", TimeSpan.FromSeconds(2));
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_f)
            {
                if (_overlay != null)
                    _overlay.FpsDisplayEnabled = !_overlay.FpsDisplayEnabled;
                return;
            }
            
            // DIP Switch controls (F1-F3)
            if (key == SDL.SDL_Keycode.SDLK_F1)
            {
                _settings.CycleLives();
                ApplyDipSwitches();
                _settings.Save();
                _overlay?.ShowMessage($"lives:{_settings.ActualLives}", TimeSpan.FromSeconds(2));
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F2)
            {
                _settings.ToggleBonusLife();
                ApplyDipSwitches();
                _settings.Save();
                _overlay?.ShowMessage($"bonus:{_settings.BonusLifeThreshold}", TimeSpan.FromSeconds(2));
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F3)
            {
                _settings.ToggleCoinInfo();
                ApplyDipSwitches();
                _settings.Save();
                _overlay?.ShowMessage(_settings.CoinInfoHidden ? "coininfo:off" : "coininfo:on", TimeSpan.FromSeconds(2));
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F4)
            {
                if (_overlay != null)
                {
                    _overlay.FpsWarningEnabled = !_overlay.FpsWarningEnabled;
                    _overlay.ShowMessage(_overlay.FpsWarningEnabled ? "fpswarning:on" : "fpswarning:off", TimeSpan.FromSeconds(2));
                }
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F5)
            {
                if (_overlay != null)
                {
                    _overlay.DipSwitchOverlayEnabled = !_overlay.DipSwitchOverlayEnabled;
                }
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_h)
            {
                if (_overlay != null)
                {
                    _overlay.ControlsOverlayEnabled = !_overlay.ControlsOverlayEnabled;
                }
                return;
            }
            
            uint keyValue = GetKeyValue(key);
            if (keyValue == 99) return; // Unknown key
            
            KeyPressed(keyValue);
        }

        /// <summary>
        /// Handles keyboard key release events for game controls.
        /// </summary>
        private void HandleKeyUp(SDL.SDL_Keycode key)
        {
            uint keyValue = GetKeyValue(key);
            if (keyValue == 99) return; // Unknown key
            
            KeyLifted(keyValue);
        }

        /// <summary>
        /// Maps SDL keycodes to internal key values for game input.
        /// </summary>
        private static uint GetKeyValue(SDL.SDL_Keycode key)
        {
            return key switch
            {
                SDL.SDL_Keycode.SDLK_c => 1,      // Coin
                SDL.SDL_Keycode.SDLK_1 => 2,      // 1P Start
                SDL.SDL_Keycode.SDLK_2 => 3,      // 2P Start
                SDL.SDL_Keycode.SDLK_LEFT => 4,   // 1P Left
                SDL.SDL_Keycode.SDLK_RIGHT => 5,  // 1P Right
                SDL.SDL_Keycode.SDLK_SPACE => 6,  // 1P Fire
                SDL.SDL_Keycode.SDLK_a => 7,      // 2P Left
                SDL.SDL_Keycode.SDLK_d => 8,      // 2P Right
                SDL.SDL_Keycode.SDLK_w => 9,      // 2P Fire
                SDL.SDL_Keycode.SDLK_i => 10,     // Easter Egg Part 1
                SDL.SDL_Keycode.SDLK_o => 11,     // Easter Egg Part 2
                SDL.SDL_Keycode.SDLK_t => 12,     // Tilt
                _ => 99                            // Unknown
            };
        }

        /// <summary>
        /// Background thread that monitors output ports and triggers sound playback.
        /// </summary>
        private void SoundThread()
        {
            byte prevPort3 = new();
            byte prevPort5 = new();

            while (!_soundLoop.IsCancellationRequested)
            {
                _cpu!.SoundTiming.WaitOne();
                if (_soundEnabled && prevPort3 != _cpu!.PortOut[3])
                {
                    if (((_cpu.PortOut[3] & 0x01) == 0x01) && ((_cpu.PortOut[3] & 0x01) != (prevPort3 & 0x01)))
                        AudioPlaybackEngine.Instance.PlaySound(_ufoLowpitch);
                    if (((_cpu.PortOut[3] & 0x02) == 0x02) && ((_cpu.PortOut[3] & 0x02) != (prevPort3 & 0x02)))
                        AudioPlaybackEngine.Instance.PlaySound(_shoot);
                    if (((_cpu.PortOut[3] & 0x04) == 0x04) && ((_cpu.PortOut[3] & 0x04) != (prevPort3 & 0x04)))
                        AudioPlaybackEngine.Instance.PlaySound(_explosion);
                    if (((_cpu.PortOut[3] & 0x08) == 0x08) && ((_cpu.PortOut[3] & 0x08) != (prevPort3 & 0x08)))
                        AudioPlaybackEngine.Instance.PlaySound(_invaderkilled);
                    if (((_cpu.PortOut[3] & 0x08) == 0x08) && ((_cpu.PortOut[3] & 0x10) != (prevPort3 & 0x10)))
                        AudioPlaybackEngine.Instance.PlaySound(_extendedplay);
                }
                prevPort3 = _cpu!.PortOut[3];

                if (_soundEnabled && prevPort5 != _cpu.PortOut[5])
                {
                    if (((_cpu.PortOut[5] & 0x01) == 0x01) && ((_cpu.PortOut[5] & 0x01) != (prevPort5 & 0x01)))
                        AudioPlaybackEngine.Instance.PlaySound(_fastinvader1);
                    if (((_cpu.PortOut[5] & 0x02) == 0x02) && ((_cpu.PortOut[5] & 0x02) != (prevPort5 & 0x02)))
                        AudioPlaybackEngine.Instance.PlaySound(_fastinvader2);
                    if (((_cpu.PortOut[5] & 0x04) == 0x04) && ((_cpu.PortOut[5] & 0x04) != (prevPort5 & 0x04)))
                        AudioPlaybackEngine.Instance.PlaySound(_fastinvader3);
                    if (((_cpu.PortOut[5] & 0x08) == 0x08) && ((_cpu.PortOut[5] & 0x08) != (prevPort5 & 0x08)))
                        AudioPlaybackEngine.Instance.PlaySound(_fastinvader4);
                    if (((_cpu.PortOut[5] & 0x10) == 0x10) && ((_cpu.PortOut[5] & 0x10) != (prevPort5 & 0x10)))
                        AudioPlaybackEngine.Instance.PlaySound(_explosion);
                }
                prevPort5 = _cpu!.PortOut[5];
                Thread.Sleep(4);
            }
       }

        /// <summary>
        /// Sets the appropriate input port bits when a key is pressed.
        /// </summary>
        private void KeyPressed(uint key)
        {
            switch (key)
            {
                case 1: // Coin
                    _inputPorts[1] |= 0x01;
                    break;

                case 2: // 1P Start
                    _inputPorts[1] |= 0x04;
                    break;

                case 3: // 2P start
                    _inputPorts[1] |= 0x02;
                    break;

                case 4: // 1P Left
                    _inputPorts[1] |= 0x20;
                    break;

                case 5: // 1P Right
                    _inputPorts[1] |= 0x40;
                    break;

                case 6: // 1P Fire
                    _inputPorts[1] |= 0x10;
                    break;

                case 7: // 2P Left
                    _inputPorts[2] |= 0x20;
                    break;

                case 8: // 2P Right
                    _inputPorts[2] |= 0x40;
                    break;

                case 9: // 2P Fire
                    _inputPorts[2] |= 0x10;
                    break;

                case 10: // Easter Egg Part 1
                    _inputPorts[1] += 0x72;
                    break;

                case 11: // Easter Egg Part 2
                    _inputPorts[1] += 0x34;
                    break;

                case 12: // Tilt
                    _inputPorts[2] += 0x04;
                    break;
            }
        }

        /// <summary>
        /// Clears the appropriate input port bits when a key is released.
        /// </summary>
        private void KeyLifted(uint key)
        {
            switch (key)
            {
                case 1: // Coin
                    _inputPorts[1] &= 0xFE;
                    break;

                case 2: // 1P Start
                    _inputPorts[1] &= 0xFB;
                    break;

                case 3: // 2P start
                    _inputPorts[1] &= 0xFD;
                    break;

                case 4: // 1P Left
                    _inputPorts[1] &= 0xDF;
                    break;

                case 5: // 1P Right
                    _inputPorts[1] &= 0xBF;
                    break;

                case 6: // 1P Fire
                    _inputPorts[1] &= 0xEF;
                    break;

                case 7: // 2P Left
                    _inputPorts[2] &= 0xDF;
                    break;

                case 8: // 2P Right
                    _inputPorts[2] &= 0xBF;
                    break;

                case 9: // 2P Fire
                    _inputPorts[2] &= 0xEF;
                    break;

                case 10: // Easter Egg Part 1
                    _inputPorts[1] &= 0x8D;
                    break;

                case 11: // Easter Egg Part 2
                    _inputPorts[1] &= 0xCB;
                    break;

                case 12: // Tilt
                    _inputPorts[2] &= 0xFB;
                    break;
            }
        }
    }
}