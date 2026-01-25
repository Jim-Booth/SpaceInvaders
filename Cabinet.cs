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

namespace SpaceInvaders
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
        private int _screenMultiplier = 2;
        private readonly object _resizeLock = new();
        
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

        // SDL2 Color values (RGBA)
        private static readonly SDL.SDL_Color _greenColor = new() { r = 0x0F, g = 0xDF, b = 0x0F, a = 0xC0 };
        private static readonly SDL.SDL_Color _whiteColor = new() { r = 0xEF, g = 0xEF, b = 0xFF, a = 0xC0 };
        private static readonly SDL.SDL_Color _whiteColor2 = new() { r = 0xEF, g = 0xEF, b = 0xFF, a = 0xF0 };
        private static readonly SDL.SDL_Color _redColor = new() { r = 0xFF, g = 0x00, b = 0x40, a = 0xC0 };

        public Cabinet()
        {
            _settings = GameSettings.Load();
            ApplyDipSwitches();
            _pixelBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
            InitializeSDL();
            LoadBackgroundTexture();
            _crtEffects = new CrtEffects(_renderer, ScreenWidth, ScreenHeight, _screenMultiplier);
            _overlay = new OverlayRenderer(_renderer);
        }

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

        private void ResizeDisplay(int newMultiplier)
        {
            if (newMultiplier < 1 || newMultiplier > 4 || newMultiplier == _screenMultiplier)
                return;

            lock (_resizeLock)
            {
                _screenMultiplier = newMultiplier;
                
                // Destroy old texture
                if (_texture != IntPtr.Zero)
                    SDL.SDL_DestroyTexture(_texture);
                
                // Recreate pixel buffer
                _pixelBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
                
                // Recreate texture
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
                
                // Resize window and re-center
                SDL.SDL_SetWindowSize(_window, ScreenWidth * _screenMultiplier, ScreenHeight * _screenMultiplier);
                SDL.SDL_SetWindowPosition(_window, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED);
            }
        }

        private void InitializeSDL()
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                throw new Exception($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");
            }

            _window = SDL.SDL_CreateWindow(
                "Space Invaders - Taito",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                ScreenWidth * _screenMultiplier,
                ScreenHeight * _screenMultiplier,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
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

            // Create streaming texture for pixel-perfect rendering
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

        public void Start()
        {
            ExecuteSpaceInvaders();
            
            // Monitor for SDL events
            Console.WriteLine("Controls: C=Coin, 1=1P Start, 2=2P Start, Arrows=Move, Space=Fire, P=Pause, ESC=Exit");
            Console.WriteLine("Display:  [/]=Scale, B=Background, R=CRT Effects, S=Sound, F=FPS");
            Console.WriteLine("DIP:      F1=Lives, F2=Bonus Life, F3=Coin Info");
            SDL.SDL_Event sdlEvent;
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Process SDL events
                while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                {
                    if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        Console.WriteLine("\nWindow closed. Exiting...");
                        _cancellationTokenSource.Cancel();
                        _cpu?.Stop();
                        break;
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
                
                try
                {
                    Task.Delay(16, _cancellationTokenSource.Token).Wait(); // ~60 FPS event polling
                }
                catch (AggregateException) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Expected when cancellation is triggered
                }
            }
            
            // Save high score before shutdown
            if (_cpu != null)
            {
                int currentHighScore = _cpu.Memory.ReadHighScore();
                Console.WriteLine($"Current high score in memory: {currentHighScore}, Saved: {_settings.HighScore}");
                if (currentHighScore > _settings.HighScore)
                {
                    _settings.HighScore = currentHighScore;
                    Console.WriteLine($"New high score! Saving: {currentHighScore}");
                }
                _settings.Save();
            }
            
            // Wait for threads to finish
            Console.WriteLine("Waiting for threads to terminate...");
            _cpuThread?.Join(2000);
            _portThread?.Join(1000);
            _displayThread?.Join(1000);
            _soundThread?.Join(1000);
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
            
            Console.WriteLine("Cleanup complete. Exiting.");
        }

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
                Console.WriteLine($"Restored high score: {_settings.HighScore}");
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

        public void DisplayThread()
        {
            while (!_displayLoop.IsCancellationRequested)
            {
                // Use timeout so we can still render overlay when paused
                bool signaled = _cpu!.DisplayTiming.WaitOne(16);
                
                // If paused and not signaled, still render the pause overlay
                if (_gamePaused && !signaled)
                {
                    lock (_resizeLock)
                    {
                        int scaledWidth = ScreenWidth * _screenMultiplier;
                        int scaledHeight = ScreenHeight * _screenMultiplier;
                        
                        // Just re-render current state with overlay
                        SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
                        SDL.SDL_RenderClear(_renderer);
                        
                        if (_backgroundEnabled && _backgroundTexture != IntPtr.Zero)
                            SDL.SDL_RenderCopy(_renderer, _backgroundTexture, IntPtr.Zero, IntPtr.Zero);
                        
                        SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
                        
                        if (_crtEffects != null && _crtEffects.Enabled)
                        {
                            _crtEffects.RenderOverlays(_renderer, scaledWidth, scaledHeight, _screenMultiplier);
                        }
                        
                        _overlay?.DrawMessage(scaledWidth, scaledHeight, _screenMultiplier);
                        
                        SDL.SDL_RenderPresent(_renderer);
                    }
                    continue;
                }
                
                lock (_resizeLock)
                {
                    try
                    {
                    // Apply phosphor persistence (fade previous frame) or clear
                    if (_crtEffects != null && _crtEffects.Enabled)
                    {
                        _crtEffects.ApplyPersistence(_pixelBuffer);
                    }
                    else
                    {
                        // Clear pixel buffer (fully transparent - alpha = 0)
                        Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);
                    }

                    int ptr = 0;
                    int scaledWidth = ScreenWidth * _screenMultiplier;
                    int scaledHeight = ScreenHeight * _screenMultiplier;
                    for (int x = 0; x < scaledWidth; x += _screenMultiplier)
                    {
                        for (int y = scaledHeight; y > 0; y -= 8 * _screenMultiplier)
                        {
                            byte value = _cpu.Video[ptr++];
                            for (int b = 0; b < 8; b++)
                            {
                                if ((value & (1 << b)) != 0)
                                {
                                    int pixelY = y - (b * _screenMultiplier);
                                    uint colorValue = GetColorValue(x, y);
                                    int bufferIndex = pixelY * scaledWidth + x;
                                    if (bufferIndex >= 0 && bufferIndex < _pixelBuffer.Length)
                                    {
                                        for (int dy = 0; dy < _screenMultiplier; dy++)
                                        {
                                            if (pixelY + dy < scaledHeight)
                                            {
                                                for (int dx = 0; dx < _screenMultiplier; dx++)
                                                {
                                                    _pixelBuffer[bufferIndex + (dy * scaledWidth) + dx] = colorValue;
                                                }
                                            }
                                        }
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

                    // Update texture with pixel buffer
                    unsafe
                    {
                        fixed (uint* pixels = _pixelBuffer)
                        {
                            SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = scaledWidth, h = scaledHeight };
                            SDL.SDL_UpdateTexture(_texture, ref fullRect, (IntPtr)pixels, scaledWidth * sizeof(uint));
                        }
                    }

                    // Clear renderer to black first
                    SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
                    SDL.SDL_RenderClear(_renderer);
                    
                    // Render background texture if enabled
                    if (_backgroundEnabled && _backgroundTexture != IntPtr.Zero)
                    {
                        SDL.SDL_RenderCopy(_renderer, _backgroundTexture, IntPtr.Zero, IntPtr.Zero);
                    }
                    
                    // Render game texture on top (with alpha blending - transparent pixels show background)
                    SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
                    
                    if (_crtEffects != null && _crtEffects.Enabled)
                    {
                        _crtEffects.RenderOverlays(_renderer, scaledWidth, scaledHeight, _screenMultiplier);
                    }
                    
                    // Draw overlay message if active
                    _overlay?.DrawMessage(scaledWidth, scaledHeight, _screenMultiplier);
                    
                    // Update and draw FPS counter
                    _overlay?.UpdateFps();
                    _overlay?.DrawFpsCounter(scaledWidth, _screenMultiplier);
                    
                    SDL.SDL_RenderPresent(_renderer);
                    }
                    catch { }
                }
            }
        }

        private uint GetColorValue(int screenPos_X, int screenPos_Y)
        {
            // Convert SDL_Color to ARGB8888 format (0xAARRGGBB)
            // Base values are for 1x resolution, scaled by _screenMultiplier
            SDL.SDL_Color color;
            if (screenPos_Y < 239 * _screenMultiplier && screenPos_Y > 195 * _screenMultiplier)
                color = _greenColor;
            else if (screenPos_Y < 256 * _screenMultiplier && screenPos_Y > 240 * _screenMultiplier && screenPos_X > 0 && screenPos_X < 127 * _screenMultiplier)
                color = _greenColor;
            else if (screenPos_Y < 256 * _screenMultiplier && screenPos_Y > 240 * _screenMultiplier)
                color = _whiteColor2;
            else if (screenPos_Y < 64 * _screenMultiplier && screenPos_Y > 32 * _screenMultiplier)
                color = _redColor;
            else
                color = _whiteColor;
            
            return ((uint)color.a << 24) | ((uint)color.r << 16) | ((uint)color.g << 8) | color.b;
        }

        private void HandleKeyDown(SDL.SDL_Keycode key)
        {
            if (key == SDL.SDL_Keycode.SDLK_ESCAPE)
            {
                Console.WriteLine("\nEscape pressed. Exiting...");
                _cancellationTokenSource.Cancel();
                _cpu?.Stop();
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_LEFTBRACKET)
            {
                ResizeDisplay(_screenMultiplier - 1);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_RIGHTBRACKET)
            {
                ResizeDisplay(_screenMultiplier + 1);
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
            
            uint keyValue = GetKeyValue(key);
            if (keyValue == 99) return; // Unknown key
            
            KeyPressed(keyValue);
        }

        private void HandleKeyUp(SDL.SDL_Keycode key)
        {
            uint keyValue = GetKeyValue(key);
            if (keyValue == 99) return; // Unknown key
            
            KeyLifted(keyValue);
        }

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