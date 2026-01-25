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
        
        // CRT curvature settings
        private readonly float BarrelDistortion = 0.15f;  // Subtle barrel distortion
        private readonly float CornerRadius = 0.08f;      // Rounded corner radius (as fraction of screen)
        private bool _crtEffectEnabled = true;
        
        // Phosphor persistence settings (ghosting/trails)
        private readonly float PhosphorDecay = 0.75f;     // How much of previous frame remains (0.0-1.0)
        private bool _phosphorPersistenceEnabled = true;
        private uint[] _persistenceBuffer = [];             // Stores fading pixel data
        
        // Additional CRT effects
        private readonly float FlickerIntensity = 0.02f;  // 2% brightness variation
        private readonly float JitterProbability = 0.002f; // 0.5% chance of jitter per frame
        private readonly int JitterMaxPixels = 1;        // Maximum horizontal jitter
        private readonly float WarmupDuration = 2.0f;     // Seconds to reach full brightness
        private readonly float BlurStrength = 0.3f;       // Horizontal blur blend factor
        private readonly Random _crtRandom = new();
        private DateTime _startupTime;
        
        private IntPtr _window;
        private IntPtr _renderer;
        private IntPtr _texture;
        private IntPtr _backgroundTexture;
        private IntPtr _vignetteTexture;
        private IntPtr _screenMaskTexture;
        private bool _backgroundEnabled = true;
        private bool _soundEnabled = true;
        private bool _gamePaused = false;
        private string? _overlayMessage = null;
        private DateTime _overlayMessageEndTime;
        private uint[] _pixelBuffer;
        
        // FPS counter
        private bool _fpsDisplayEnabled = false;
        private int _frameCount = 0;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private double _currentFps = 0.0;
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
            _startupTime = DateTime.Now;
            _pixelBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
            _persistenceBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
            InitializeSDL();
            LoadBackgroundTexture();
            CreateVignetteTexture();
            CreateScreenMaskTexture();
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

        private void CreateVignetteTexture()
        {
            int width = ScreenWidth * _screenMultiplier;
            int height = ScreenHeight * _screenMultiplier;
            
            // Destroy old vignette texture if it exists
            if (_vignetteTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_vignetteTexture);
            
            // Create a streaming texture for the vignette
            _vignetteTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                width,
                height
            );
            
            if (_vignetteTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(_vignetteTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            // Generate vignette pattern
            uint[] vignetteBuffer = new uint[width * height];
            float centerX = width / 2.0f;
            float centerY = height / 2.0f;
            float maxDist = (float)Math.Sqrt(centerX * centerX + centerY * centerY);
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float normalizedDist = dist / maxDist;
                    
                    // Vignette darkening: stronger at edges (quadratic falloff)
                    float vignette = normalizedDist * normalizedDist * 0.6f;
                    byte alpha = (byte)(Math.Min(vignette * 255, 180));
                    
                    // ARGB format: black with varying alpha
                    vignetteBuffer[y * width + x] = ((uint)alpha << 24) | 0x000000;
                }
            }
            
            // Upload to texture
            unsafe
            {
                fixed (uint* pixels = vignetteBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = width, h = height };
                    SDL.SDL_UpdateTexture(_vignetteTexture, ref fullRect, (IntPtr)pixels, width * sizeof(uint));
                }
            }
        }

        private void CreateScreenMaskTexture()
        {
            int width = ScreenWidth * _screenMultiplier;
            int height = ScreenHeight * _screenMultiplier;
            
            // Destroy old mask texture if it exists
            if (_screenMaskTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_screenMaskTexture);
            
            // Create a streaming texture for the screen mask
            _screenMaskTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                width,
                height
            );
            
            if (_screenMaskTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(_screenMaskTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            // Generate screen mask with rounded corners and edge darkening
            uint[] maskBuffer = new uint[width * height];
            float centerX = width / 2.0f;
            float centerY = height / 2.0f;
            
            // Corner radius in pixels
            float cornerRadius = Math.Min(width, height) * CornerRadius;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Normalize coordinates to -1 to 1 range
                    float nx = (x - centerX) / centerX;
                    float ny = (y - centerY) / centerY;
                    
                    // Check rounded corners using superellipse formula
                    // This creates the characteristic rounded rectangle of CRT screens
                    float cornerX = Math.Max(0, Math.Abs(x - centerX) - (centerX - cornerRadius));
                    float cornerY = Math.Max(0, Math.Abs(y - centerY) - (centerY - cornerRadius));
                    float cornerDist = (float)Math.Sqrt(cornerX * cornerX + cornerY * cornerY);
                    
                    // Calculate barrel distortion factor
                    float r2 = nx * nx + ny * ny;
                    float barrelFactor = 1.0f + BarrelDistortion * r2;
                    
                    // Edge darkening based on distance from center (simulates CRT curvature)
                    float edgeDark = r2 * 0.3f;
                    
                    byte alpha;
                    if (cornerDist > cornerRadius)
                    {
                        // Outside rounded corner - fully black/opaque
                        alpha = 255;
                    }
                    else if (cornerDist > cornerRadius - 2)
                    {
                        // Anti-aliased edge of rounded corner
                        float t = (cornerDist - (cornerRadius - 2)) / 2.0f;
                        alpha = (byte)(t * 255);
                    }
                    else
                    {
                        // Inside screen area - apply subtle edge darkening from barrel effect
                        alpha = (byte)(Math.Min(edgeDark * 60, 40));
                    }
                    
                    // ARGB format: black with varying alpha
                    maskBuffer[y * width + x] = ((uint)alpha << 24) | 0x000000;
                }
            }
            
            // Upload to texture
            unsafe
            {
                fixed (uint* pixels = maskBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = width, h = height };
                    SDL.SDL_UpdateTexture(_screenMaskTexture, ref fullRect, (IntPtr)pixels, width * sizeof(uint));
                }
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
                _persistenceBuffer = new uint[(ScreenWidth * _screenMultiplier) * (ScreenHeight * _screenMultiplier)];
                
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
                
                // Recreate vignette and screen mask textures for new size
                CreateVignetteTexture();
                CreateScreenMaskTexture();
                
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
            if (_texture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_texture);
            if (_backgroundTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_backgroundTexture);
            if (_vignetteTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_vignetteTexture);
            if (_screenMaskTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_screenMaskTexture);
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
                        
                        if (_crtEffectEnabled)
                        {
                            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 35);
                            for (int x = 0; x < scaledWidth; x += _screenMultiplier)
                                SDL.SDL_RenderDrawLine(_renderer, x, 0, x, scaledHeight);
                            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 25);
                            for (int y = 0; y < scaledHeight; y += _screenMultiplier)
                                SDL.SDL_RenderDrawLine(_renderer, 0, y, scaledWidth, y);
                            if (_vignetteTexture != IntPtr.Zero)
                                SDL.SDL_RenderCopy(_renderer, _vignetteTexture, IntPtr.Zero, IntPtr.Zero);
                            if (_screenMaskTexture != IntPtr.Zero)
                                SDL.SDL_RenderCopy(_renderer, _screenMaskTexture, IntPtr.Zero, IntPtr.Zero);
                        }
                        
                        if (_overlayMessage != null)
                            DrawOverlayMessage(scaledWidth, scaledHeight);
                        
                        SDL.SDL_RenderPresent(_renderer);
                    }
                    continue;
                }
                
                lock (_resizeLock)
                {
                    try
                    {
                    // Apply phosphor persistence (fade previous frame) or clear
                    if (_phosphorPersistenceEnabled && _crtEffectEnabled)
                    {
                        // Decay previous frame's pixels (phosphor fade effect)
                        for (int i = 0; i < _persistenceBuffer.Length; i++)
                        {
                            uint pixel = _persistenceBuffer[i];
                            if (pixel != 0)
                            {
                                // Extract ARGB components
                                byte a = (byte)((pixel >> 24) & 0xFF);
                                byte r = (byte)((pixel >> 16) & 0xFF);
                                byte g = (byte)((pixel >> 8) & 0xFF);
                                byte b = (byte)(pixel & 0xFF);
                                
                                // Apply decay to each component
                                a = (byte)(a * PhosphorDecay);
                                r = (byte)(r * PhosphorDecay);
                                g = (byte)(g * PhosphorDecay);
                                b = (byte)(b * PhosphorDecay);
                                
                                // Threshold to prevent infinite dim pixels
                                if (a < 8) a = 0;
                                if (r < 8) r = 0;
                                if (g < 8) g = 0;
                                if (b < 8) b = 0;
                                
                                _persistenceBuffer[i] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                            }
                            // Start with decayed persistence buffer
                            _pixelBuffer[i] = _persistenceBuffer[i];
                        }
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
                    if (_phosphorPersistenceEnabled && _crtEffectEnabled)
                    {
                        Array.Copy(_pixelBuffer, _persistenceBuffer, _pixelBuffer.Length);
                    }
                    
                    // Apply CRT post-processing effects
                    if (_crtEffectEnabled)
                    {
                        ApplyCrtPostProcessing(scaledWidth, scaledHeight);
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
                    
                    if (_crtEffectEnabled)
                    {
                        // Draw CRT scanlines for authentic appearance
                        SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                        
                        // Vertical scanlines (due to rotated CRT)
                        SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 35);
                        for (int x = 0; x < scaledWidth; x += _screenMultiplier)
                        {
                            SDL.SDL_RenderDrawLine(_renderer, x, 0, x, scaledHeight);
                        }
                        
                        // Horizontal scanlines (typical CRT raster lines)
                        SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 25);
                        for (int y = 0; y < scaledHeight; y += _screenMultiplier)
                        {
                            SDL.SDL_RenderDrawLine(_renderer, 0, y, scaledWidth, y);
                        }
                        
                        // Apply vignette overlay for edge darkening
                        if (_vignetteTexture != IntPtr.Zero)
                        {
                            SDL.SDL_RenderCopy(_renderer, _vignetteTexture, IntPtr.Zero, IntPtr.Zero);
                        }
                        
                        // Apply CRT screen mask with rounded corners
                        if (_screenMaskTexture != IntPtr.Zero)
                        {
                            SDL.SDL_RenderCopy(_renderer, _screenMaskTexture, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    
                    // Draw overlay message if active
                    if (_overlayMessage != null && DateTime.Now < _overlayMessageEndTime)
                    {
                        DrawOverlayMessage(scaledWidth, scaledHeight);
                    }
                    else if (_overlayMessage != null)
                    {
                        _overlayMessage = null;
                    }
                    
                    // Update and draw FPS counter
                    _frameCount++;
                    var now = DateTime.Now;
                    var elapsed = (now - _lastFpsUpdate).TotalSeconds;
                    if (elapsed >= 0.5) // Update FPS every 0.5 seconds
                    {
                        _currentFps = _frameCount / elapsed;
                        _frameCount = 0;
                        _lastFpsUpdate = now;
                    }
                    
                    if (_fpsDisplayEnabled)
                    {
                        DrawFpsCounter(scaledWidth);
                    }
                    
                    SDL.SDL_RenderPresent(_renderer);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Applies CRT post-processing effects: bloom, horizontal blur, flicker, jitter, and warmup.
        /// </summary>
        private void ApplyCrtPostProcessing(int width, int height)
        {
            // First pass: Apply bloom/glow effect
            ApplyBloomEffect(width, height);
            
            // Calculate warmup brightness (0.0 to 1.0 over WarmupDuration seconds)
            float elapsedSeconds = (float)(DateTime.Now - _startupTime).TotalSeconds;
            float warmupFactor = Math.Min(1.0f, elapsedSeconds / WarmupDuration);
            // Ease-in curve for more realistic tube warmup
            warmupFactor = warmupFactor * warmupFactor;
            
            // Calculate flicker (random brightness variation)
            float flickerFactor = 1.0f - (float)(_crtRandom.NextDouble() * FlickerIntensity);
            
            // Combined brightness factor
            float brightnessFactor = warmupFactor * flickerFactor;
            
            // Determine if this frame has horizontal jitter
            int jitterOffset = 0;
            if (_crtRandom.NextDouble() < JitterProbability)
            {
                jitterOffset = _crtRandom.Next(-JitterMaxPixels, JitterMaxPixels + 1) * _screenMultiplier;
            }
            
            // Apply effects to pixel buffer
            uint[] tempBuffer = new uint[_pixelBuffer.Length];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = y * width + x;
                    
                    // Apply jitter offset
                    int jitteredX = x + jitterOffset;
                    if (jitteredX < 0 || jitteredX >= width)
                    {
                        tempBuffer[srcIndex] = 0; // Black for out-of-bounds
                        continue;
                    }
                    
                    int jitteredIndex = y * width + jitteredX;
                    uint pixel = _pixelBuffer[jitteredIndex];
                    
                    if (pixel == 0)
                    {
                        tempBuffer[srcIndex] = 0;
                        continue;
                    }
                    
                    // Extract ARGB components
                    byte a = (byte)((pixel >> 24) & 0xFF);
                    byte r = (byte)((pixel >> 16) & 0xFF);
                    byte g = (byte)((pixel >> 8) & 0xFF);
                    byte b = (byte)(pixel & 0xFF);
                    
                    // Apply horizontal blur (blend with neighbors)
                    if (BlurStrength > 0 && x > 0 && x < width - 1)
                    {
                        uint leftPixel = _pixelBuffer[jitteredIndex - 1];
                        uint rightPixel = _pixelBuffer[jitteredIndex + 1];
                        
                        if (leftPixel != 0 || rightPixel != 0)
                        {
                            byte lr = (byte)((leftPixel >> 16) & 0xFF);
                            byte lg = (byte)((leftPixel >> 8) & 0xFF);
                            byte lb = (byte)(leftPixel & 0xFF);
                            byte rr = (byte)((rightPixel >> 16) & 0xFF);
                            byte rg = (byte)((rightPixel >> 8) & 0xFF);
                            byte rb = (byte)(rightPixel & 0xFF);
                            
                            float centerWeight = 1.0f - BlurStrength;
                            float sideWeight = BlurStrength * 0.5f;
                            
                            r = (byte)(r * centerWeight + (lr + rr) * sideWeight);
                            g = (byte)(g * centerWeight + (lg + rg) * sideWeight);
                            b = (byte)(b * centerWeight + (lb + rb) * sideWeight);
                        }
                    }
                    
                    // Apply brightness (warmup + flicker) - but preserve bloom pixels (lower alpha)
                    if (a > 100) // Full brightness pixels get warmup/flicker
                    {
                        r = (byte)(r * brightnessFactor);
                        g = (byte)(g * brightnessFactor);
                        b = (byte)(b * brightnessFactor);
                        a = (byte)(a * brightnessFactor);
                    }
                    // Bloom pixels (lower alpha) pass through without modification
                    
                    tempBuffer[srcIndex] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                }
            }
            
            // Copy back to pixel buffer
            Array.Copy(tempBuffer, _pixelBuffer, _pixelBuffer.Length);
        }

        /// <summary>
        /// Applies bloom/glow effect - bright pixels bleed light into surrounding dark areas.
        /// Uses a fast single-pass approach for performance.
        /// </summary>
        private void ApplyBloomEffect(int width, int height)
        {
            // Simple and fast: just add glow to immediate neighbors of lit pixels
            uint[] bloomBuffer = new uint[_pixelBuffer.Length];
            Array.Copy(_pixelBuffer, bloomBuffer, _pixelBuffer.Length);
            
            int step = _screenMultiplier; // Check every Nth pixel for speed
            
            for (int y = step; y < height - step; y += step)
            {
                for (int x = step; x < width - step; x += step)
                {
                    int index = y * width + x;
                    uint pixel = _pixelBuffer[index];
                    
                    if (pixel == 0) continue;
                    
                    // Extract RGB and check brightness
                    byte r = (byte)((pixel >> 16) & 0xFF);
                    byte g = (byte)((pixel >> 8) & 0xFF);
                    byte b = (byte)(pixel & 0xFF);
                    byte a = (byte)((pixel >> 24) & 0xFF);
                    
                    // Use max channel for brightness (catches saturated colors like green)
                    float brightness = Math.Max(r, Math.Max(g, b)) / 255.0f;
                    if (brightness < 0.5f) continue; // Only bright pixels glow
                    
                    // Colored glow matching the source pixel
                    byte glowR = r;
                    byte glowG = g;
                    byte glowB = b;
                    byte glowA = 120;
                    
                    int[] offsets = { -width, width, -1, 1, -width-1, -width+1, width-1, width+1 }; // All 8 neighbors
                    foreach (int offset in offsets)
                    {
                        int neighborIndex = index + offset;
                        if (neighborIndex < 0 || neighborIndex >= _pixelBuffer.Length) continue;
                        
                        uint neighbor = bloomBuffer[neighborIndex];
                        
                        // Only add glow to dark/empty pixels
                        if (neighbor == 0)
                        {
                            bloomBuffer[neighborIndex] = ((uint)glowA << 24) | ((uint)glowR << 16) | ((uint)glowG << 8) | glowB;
                        }
                    }
                }
            }
            
            // Copy bloom result back to pixel buffer
            Array.Copy(bloomBuffer, _pixelBuffer, _pixelBuffer.Length);
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
                _crtEffectEnabled = !_crtEffectEnabled;
                _phosphorPersistenceEnabled = _crtEffectEnabled;
                if (!_phosphorPersistenceEnabled)
                {
                    // Clear persistence buffer when disabling
                    Array.Clear(_persistenceBuffer, 0, _persistenceBuffer.Length);
                }
                _overlayMessage = _crtEffectEnabled ? "crt:on" : "crt:off";
                _overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_p)
            {
                _gamePaused = !_gamePaused;
                if (_cpu != null) _cpu.Paused = _gamePaused;
                _overlayMessage = _gamePaused ? "paused" : null;
                _overlayMessageEndTime = _gamePaused ? DateTime.MaxValue : DateTime.Now;
                return;
            }
            
            
            if (key == SDL.SDL_Keycode.SDLK_s)
            {
                _soundEnabled = !_soundEnabled;
                _overlayMessage = _soundEnabled ? "sound:on" : "sound:off";
                _overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_f)
            {
                _fpsDisplayEnabled = !_fpsDisplayEnabled;
                return;
            }
            
            // DIP Switch controls (F1-F3)
            if (key == SDL.SDL_Keycode.SDLK_F1)
            {
                _settings.CycleLives();
                ApplyDipSwitches();
                _settings.Save();
                _overlayMessage = $"lives:{_settings.ActualLives}";
                _overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F2)
            {
                _settings.ToggleBonusLife();
                ApplyDipSwitches();
                _settings.Save();
                _overlayMessage = $"bonus:{_settings.BonusLifeThreshold}";
                _overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F3)
            {
                _settings.ToggleCoinInfo();
                ApplyDipSwitches();
                _settings.Save();
                _overlayMessage = _settings.CoinInfoHidden ? "coininfo:off" : "coininfo:on";
                _overlayMessageEndTime = DateTime.Now.AddSeconds(2);
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

        private void DrawOverlayMessage(int screenWidth, int screenHeight)
        {
            if (_overlayMessage == null) return;
            
            // Character dimensions (scaled)
            int charWidth = 5 * _screenMultiplier;
            int charHeight = 7 * _screenMultiplier;
            int charSpacing = 1 * _screenMultiplier;
            int totalWidth = _overlayMessage.Length * (charWidth + charSpacing) - charSpacing;
            
            // Center position
            int startX = (screenWidth - totalWidth) / 2;
            int startY = (screenHeight - charHeight) / 2;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 180);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 10 * _screenMultiplier,
                y = startY - 5 * _screenMultiplier,
                w = totalWidth + 20 * _screenMultiplier,
                h = charHeight + 10 * _screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw each character
            SDL.SDL_SetRenderDrawColor(_renderer, 0xFF, 0xFF, 0x00, 0xFF); // Yellow text
            for (int i = 0; i < _overlayMessage.Length; i++)
            {
                int charX = startX + i * (charWidth + charSpacing);
                DrawChar(_overlayMessage[i], charX, startY, _screenMultiplier);
            }
        }

        private void DrawFpsCounter(int screenWidth)
        {
            // Format FPS string
            string fpsText = $"fps:{_currentFps:F1}";
            
            // Character dimensions (scaled)
            int charWidth = 5 * _screenMultiplier;
            int charHeight = 7 * _screenMultiplier;
            int charSpacing = 1 * _screenMultiplier;
            int totalWidth = fpsText.Length * (charWidth + charSpacing) - charSpacing;
            
            // Position in top-right corner with padding
            int padding = 5 * _screenMultiplier;
            int startX = screenWidth - totalWidth - padding;
            int startY = padding;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 150);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 3 * _screenMultiplier,
                y = startY - 2 * _screenMultiplier,
                w = totalWidth + 6 * _screenMultiplier,
                h = charHeight + 4 * _screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw FPS text in green
            SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0x00, 0xFF);
            for (int i = 0; i < fpsText.Length; i++)
            {
                int charX = startX + i * (charWidth + charSpacing);
                DrawChar(fpsText[i], charX, startY, _screenMultiplier);
            }
        }

        private void DrawChar(char c, int x, int y, int scale)
        {
            // Complete 5x7 pixel font for all alphanumeric characters and symbols
            // Bit 4 = leftmost pixel, bit 0 = rightmost pixel
            byte[] pattern = c switch
            {
                // Lowercase letters
                'a' => [0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
                'b' => [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E],
                'c' => [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
                'd' => [0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C],
                'e' => [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F],
                'f' => [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
                'g' => [0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F],
                'h' => [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
                'i' => [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E],
                'j' => [0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C],
                'k' => [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11],
                'l' => [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
                'm' => [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11],
                'n' => [0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11],
                'o' => [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
                'p' => [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
                'q' => [0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D],
                'r' => [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
                's' => [0x0E, 0x11, 0x10, 0x0E, 0x01, 0x11, 0x0E],
                't' => [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
                'u' => [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
                'v' => [0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04],
                'w' => [0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11],
                'x' => [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
                'y' => [0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04],
                'z' => [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
                
                // Numbers
                '0' => [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
                '1' => [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
                '2' => [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
                '3' => [0x0E, 0x11, 0x01, 0x06, 0x01, 0x11, 0x0E],
                '4' => [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
                '5' => [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
                '6' => [0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E],
                '7' => [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08],
                '8' => [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
                '9' => [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C],
                
                // Symbols
                ':' => [0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00],
                '.' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C],
                '!' => [0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04],
                '?' => [0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04],
                '-' => [0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00],
                '+' => [0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00],
                '/' => [0x01, 0x02, 0x02, 0x04, 0x08, 0x08, 0x10],
                ' ' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
                
                _ => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
            };
            
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    if ((pattern[row] & (0x10 >> col)) != 0)
                    {
                        SDL.SDL_Rect pixelRect = new SDL.SDL_Rect
                        {
                            x = x + col * scale,
                            y = y + row * scale,
                            w = scale,
                            h = scale
                        };
                        SDL.SDL_RenderFillRect(_renderer, ref pixelRect);
                    }
                }
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