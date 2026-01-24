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
        private Intel_8080? cpu;
        private Thread? port_thread;
        private Thread? cpu_thread;
        private Thread? display_thread;
        private Thread? sound_thread;

        private static readonly CancellationTokenSource CancellationTokenSource = new();
        private static readonly CancellationToken displayLoop = CancellationTokenSource.Token;
        private static readonly CancellationToken soundLoop = CancellationTokenSource.Token;
        private static readonly CancellationToken portLoop = CancellationTokenSource.Token;

        private readonly byte[] inputPorts = [0x0E, 0x08, 0x00, 0x00];
        private readonly GameSettings settings;
        private readonly int SCREEN_WIDTH = 223;
        private readonly int SCREEN_HEIGHT = 256;
        private int SCREEN_MULTIPLIER = 2;
        private readonly object resizeLock = new();
        
        // CRT curvature settings
        private readonly float BARREL_DISTORTION = 0.15f;  // Subtle barrel distortion
        private readonly float CORNER_RADIUS = 0.08f;      // Rounded corner radius (as fraction of screen)
        private bool crtEffectEnabled = true;
        
        // Phosphor persistence settings (ghosting/trails)
        private readonly float PHOSPHOR_DECAY = 0.75f;     // How much of previous frame remains (0.0-1.0)
        private bool phosphorPersistenceEnabled = true;
        private uint[] persistenceBuffer = [];             // Stores fading pixel data
        
        // Additional CRT effects
        private readonly float FLICKER_INTENSITY = 0.02f;  // 2% brightness variation
        private readonly float JITTER_PROBABILITY = 0.002f; // 0.5% chance of jitter per frame
        private readonly int JITTER_MAX_PIXELS = 1;        // Maximum horizontal jitter
        private readonly float WARMUP_DURATION = 2.0f;     // Seconds to reach full brightness
        private readonly float BLUR_STRENGTH = 0.3f;       // Horizontal blur blend factor
        private readonly Random crtRandom = new();
        private DateTime startupTime;
        
        private IntPtr window;
        private IntPtr renderer;
        private IntPtr texture;
        private IntPtr backgroundTexture;
        private IntPtr vignetteTexture;
        private IntPtr screenMaskTexture;
        private bool backgroundEnabled = true;
        private bool soundEnabled = true;
        private bool gamePaused = false;
        private string? overlayMessage = null;
        private DateTime overlayMessageEndTime;
        private uint[] pixelBuffer;
        private static readonly string appPath = AppDomain.CurrentDomain.BaseDirectory;

        private readonly CachedSound ufo_lowpitch = new(Path.Combine(appPath, "SOUNDS", "ufo_lowpitch.wav"));
        private readonly CachedSound shoot = new(Path.Combine(appPath, "SOUNDS", "shoot.wav"));
        private readonly CachedSound invaderkilled = new(Path.Combine(appPath, "SOUNDS", "invaderkilled.wav"));
        private readonly CachedSound fastinvader1 = new(Path.Combine(appPath, "SOUNDS", "fastinvader1.wav"));
        private readonly CachedSound fastinvader2 = new(Path.Combine(appPath, "SOUNDS", "fastinvader2.wav"));
        private readonly CachedSound fastinvader3 = new(Path.Combine(appPath, "SOUNDS", "fastinvader3.wav"));
        private readonly CachedSound fastinvader4 = new(Path.Combine(appPath, "SOUNDS", "fastinvader4.wav"));
        private readonly CachedSound explosion = new(Path.Combine(appPath, "SOUNDS", "explosion.wav"));
        private readonly CachedSound extendedplay = new(Path.Combine(appPath, "SOUNDS", "extendedPlay.wav"));

        // SDL2 Color values (RGBA)
        private static readonly SDL.SDL_Color greenColor = new() { r = 0x0F, g = 0xDF, b = 0x0F, a = 0xC0 };
        private static readonly SDL.SDL_Color whiteColor = new() { r = 0xEF, g = 0xEF, b = 0xFF, a = 0xC0 };
        private static readonly SDL.SDL_Color whiteColor2 = new() { r = 0xEF, g = 0xEF, b = 0xFF, a = 0xF0 };
        private static readonly SDL.SDL_Color redColor = new() { r = 0xFF, g = 0x00, b = 0x40, a = 0xC0 };

        public Cabinet()
        {
            settings = GameSettings.Load();
            ApplyDipSwitches();
            startupTime = DateTime.Now;
            pixelBuffer = new uint[(SCREEN_WIDTH * SCREEN_MULTIPLIER) * (SCREEN_HEIGHT * SCREEN_MULTIPLIER)];
            persistenceBuffer = new uint[(SCREEN_WIDTH * SCREEN_MULTIPLIER) * (SCREEN_HEIGHT * SCREEN_MULTIPLIER)];
            InitializeSDL();
            LoadBackgroundTexture();
            CreateVignetteTexture();
            CreateScreenMaskTexture();
        }

        private void LoadBackgroundTexture()
        {
            string backgroundPath = Path.Combine(appPath, "Cabinet.bmp");
            if (!File.Exists(backgroundPath))
            {
                return;
            }

            IntPtr surface = SDL.SDL_LoadBMP(backgroundPath);
            
            if (surface == IntPtr.Zero)
            {
                return;
            }

            backgroundTexture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            SDL.SDL_FreeSurface(surface);

            if (backgroundTexture != IntPtr.Zero)
            {
                // Ensure background renders without blending (fully opaque)
                SDL.SDL_SetTextureBlendMode(backgroundTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_NONE);
            }
        }

        private void CreateVignetteTexture()
        {
            int width = SCREEN_WIDTH * SCREEN_MULTIPLIER;
            int height = SCREEN_HEIGHT * SCREEN_MULTIPLIER;
            
            // Destroy old vignette texture if it exists
            if (vignetteTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(vignetteTexture);
            
            // Create a streaming texture for the vignette
            vignetteTexture = SDL.SDL_CreateTexture(
                renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                width,
                height
            );
            
            if (vignetteTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(vignetteTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
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
                    SDL.SDL_UpdateTexture(vignetteTexture, ref fullRect, (IntPtr)pixels, width * sizeof(uint));
                }
            }
        }

        private void CreateScreenMaskTexture()
        {
            int width = SCREEN_WIDTH * SCREEN_MULTIPLIER;
            int height = SCREEN_HEIGHT * SCREEN_MULTIPLIER;
            
            // Destroy old mask texture if it exists
            if (screenMaskTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(screenMaskTexture);
            
            // Create a streaming texture for the screen mask
            screenMaskTexture = SDL.SDL_CreateTexture(
                renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                width,
                height
            );
            
            if (screenMaskTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(screenMaskTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            // Generate screen mask with rounded corners and edge darkening
            uint[] maskBuffer = new uint[width * height];
            float centerX = width / 2.0f;
            float centerY = height / 2.0f;
            
            // Corner radius in pixels
            float cornerRadius = Math.Min(width, height) * CORNER_RADIUS;
            
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
                    float barrelFactor = 1.0f + BARREL_DISTORTION * r2;
                    
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
                    SDL.SDL_UpdateTexture(screenMaskTexture, ref fullRect, (IntPtr)pixels, width * sizeof(uint));
                }
            }
        }

        private void ResizeDisplay(int newMultiplier)
        {
            if (newMultiplier < 1 || newMultiplier > 4 || newMultiplier == SCREEN_MULTIPLIER)
                return;

            lock (resizeLock)
            {
                SCREEN_MULTIPLIER = newMultiplier;
                
                // Destroy old texture
                if (texture != IntPtr.Zero)
                    SDL.SDL_DestroyTexture(texture);
                
                // Recreate pixel buffer
                pixelBuffer = new uint[(SCREEN_WIDTH * SCREEN_MULTIPLIER) * (SCREEN_HEIGHT * SCREEN_MULTIPLIER)];
                persistenceBuffer = new uint[(SCREEN_WIDTH * SCREEN_MULTIPLIER) * (SCREEN_HEIGHT * SCREEN_MULTIPLIER)];
                
                // Recreate texture
                texture = SDL.SDL_CreateTexture(
                    renderer,
                    SDL.SDL_PIXELFORMAT_ARGB8888,
                    (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                    SCREEN_WIDTH * SCREEN_MULTIPLIER,
                    SCREEN_HEIGHT * SCREEN_MULTIPLIER
                );
                
                if (texture == IntPtr.Zero)
                {
                    throw new Exception($"Texture could not be created! SDL_Error: {SDL.SDL_GetError()}");
                }
                
                // Enable alpha blending on the game texture so background shows through
                SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                
                // Recreate vignette and screen mask textures for new size
                CreateVignetteTexture();
                CreateScreenMaskTexture();
                
                // Resize window and re-center
                SDL.SDL_SetWindowSize(window, SCREEN_WIDTH * SCREEN_MULTIPLIER, SCREEN_HEIGHT * SCREEN_MULTIPLIER);
                SDL.SDL_SetWindowPosition(window, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED);
            }
        }

        private void InitializeSDL()
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                throw new Exception($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");
            }

            window = SDL.SDL_CreateWindow(
                "Space Invaders - Taito",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                SCREEN_WIDTH * SCREEN_MULTIPLIER,
                SCREEN_HEIGHT * SCREEN_MULTIPLIER,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
            );

            if (window == IntPtr.Zero)
            {
                throw new Exception($"Window could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }

            // Enable linear filtering for smooth CRT-like appearance
            SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1");

            renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (renderer == IntPtr.Zero)
            {
                throw new Exception($"Renderer could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }

            // Create streaming texture for pixel-perfect rendering
            texture = SDL.SDL_CreateTexture(
                renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                SCREEN_WIDTH * SCREEN_MULTIPLIER,
                SCREEN_HEIGHT * SCREEN_MULTIPLIER
            );
            
            if (texture == IntPtr.Zero)
            {
                throw new Exception($"Texture could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }
            
            // Enable alpha blending on the game texture so background shows through
            SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        }

        /// <summary>
        /// Applies DIP switch settings to Port 2.
        /// Preserves player input bits (4-6) while setting DIP bits (0-1, 3, 7).
        /// </summary>
        private void ApplyDipSwitches()
        {
            byte dipBits = settings.GetPort2DipBits();
            // Clear DIP switch bits (0-1, 3, 7) and preserve player input bits (2, 4-6)
            inputPorts[2] = (byte)((inputPorts[2] & 0x74) | dipBits);
        }

        public void Start()
        {
            ExecuteSpaceInvaders();
            
            // Monitor for SDL events
            Console.WriteLine("Controls: C=Coin, 1=1P Start, 2=2P Start, Arrows=Move, Space=Fire, P=Pause, ESC=Exit");
            Console.WriteLine("Display:  [/]=Scale, B=Background, R=CRT Effects, S=Sound");
            Console.WriteLine("DIP:      F1=Lives, F2=Bonus Life, F3=Coin Info");
            SDL.SDL_Event sdlEvent;
            while (!CancellationTokenSource.Token.IsCancellationRequested)
            {
                // Process SDL events
                while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                {
                    if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        Console.WriteLine("\nWindow closed. Exiting...");
                        CancellationTokenSource.Cancel();
                        cpu?.Stop();
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
                    Task.Delay(16, CancellationTokenSource.Token).Wait(); // ~60 FPS event polling
                }
                catch (AggregateException) when (CancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Expected when cancellation is triggered
                }
            }
            
            // Wait for threads to finish
            Console.WriteLine("Waiting for threads to terminate...");
            cpu_thread?.Join(2000);
            port_thread?.Join(1000);
            display_thread?.Join(1000);
            sound_thread?.Join(1000);
            AudioPlaybackEngine.Instance.Dispose();
            
            // Cleanup SDL
            if (texture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(texture);
            if (backgroundTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(backgroundTexture);
            if (vignetteTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(vignetteTexture);
            if (screenMaskTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(screenMaskTexture);
            if (renderer != IntPtr.Zero)
                SDL.SDL_DestroyRenderer(renderer);
            if (window != IntPtr.Zero)
                SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();
            
            Console.WriteLine("Cleanup complete. Exiting.");
        }

        private void ExecuteSpaceInvaders()
        {
            cpu = new Intel_8080(new Memory(0x10000));
            cpu.Memory.LoadFromFile(Path.Combine(appPath, "ROMS", "invaders.h"), 0x0000, 0x800); // invaders.h 0000 - 07FF
            cpu.Memory.LoadFromFile(Path.Combine(appPath, "ROMS", "invaders.g"), 0x0800, 0x800); // invaders.g 0800 - 0FFF
            cpu.Memory.LoadFromFile(Path.Combine(appPath, "ROMS", "invaders.f"), 0x1000, 0x800); // invaders.f 1000 - 17FF
            cpu.Memory.LoadFromFile(Path.Combine(appPath, "ROMS", "invaders.e"), 0x1800, 0x800); // invaders.e 1800 - 1FFF

            cpu_thread = new Thread(async () => 
            {
                try
                {
                    await cpu!.StartAsync(CancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                }
            })
            {
                Priority = ThreadPriority.Highest
            };
            cpu_thread.Start();

            while (!cpu.Running) { }

            port_thread = new Thread(PortThread)
            {
                IsBackground = true
            };
            port_thread.Start();

            display_thread = new Thread(DisplayThread)
            {
                IsBackground = true
            };
            display_thread.Start();

            sound_thread = new Thread(SoundThread)
            {
                IsBackground = true
            };
            sound_thread.Start();
        }

        private async void PortThread()
        {
            while (!portLoop.IsCancellationRequested)
            {
                while (cpu!.PortIn == inputPorts)
                {
                    try
                    {
                        await Task.Delay(4, portLoop);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                cpu.PortIn = inputPorts;
            }
        }

        public void DisplayThread()
        {
            while (!displayLoop.IsCancellationRequested)
            {
                // Use timeout so we can still render overlay when paused
                bool signaled = cpu!.DisplayTiming.WaitOne(16);
                
                // If paused and not signaled, still render the pause overlay
                if (gamePaused && !signaled)
                {
                    lock (resizeLock)
                    {
                        int scaledWidth = SCREEN_WIDTH * SCREEN_MULTIPLIER;
                        int scaledHeight = SCREEN_HEIGHT * SCREEN_MULTIPLIER;
                        
                        // Just re-render current state with overlay
                        SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                        SDL.SDL_RenderClear(renderer);
                        
                        if (backgroundEnabled && backgroundTexture != IntPtr.Zero)
                            SDL.SDL_RenderCopy(renderer, backgroundTexture, IntPtr.Zero, IntPtr.Zero);
                        
                        SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
                        
                        if (crtEffectEnabled)
                        {
                            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 35);
                            for (int x = 0; x < scaledWidth; x += SCREEN_MULTIPLIER)
                                SDL.SDL_RenderDrawLine(renderer, x, 0, x, scaledHeight);
                            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 25);
                            for (int y = 0; y < scaledHeight; y += SCREEN_MULTIPLIER)
                                SDL.SDL_RenderDrawLine(renderer, 0, y, scaledWidth, y);
                            if (vignetteTexture != IntPtr.Zero)
                                SDL.SDL_RenderCopy(renderer, vignetteTexture, IntPtr.Zero, IntPtr.Zero);
                            if (screenMaskTexture != IntPtr.Zero)
                                SDL.SDL_RenderCopy(renderer, screenMaskTexture, IntPtr.Zero, IntPtr.Zero);
                        }
                        
                        if (overlayMessage != null)
                            DrawOverlayMessage(scaledWidth, scaledHeight);
                        
                        SDL.SDL_RenderPresent(renderer);
                    }
                    continue;
                }
                
                lock (resizeLock)
                {
                    try
                    {
                    // Apply phosphor persistence (fade previous frame) or clear
                    if (phosphorPersistenceEnabled && crtEffectEnabled)
                    {
                        // Decay previous frame's pixels (phosphor fade effect)
                        for (int i = 0; i < persistenceBuffer.Length; i++)
                        {
                            uint pixel = persistenceBuffer[i];
                            if (pixel != 0)
                            {
                                // Extract ARGB components
                                byte a = (byte)((pixel >> 24) & 0xFF);
                                byte r = (byte)((pixel >> 16) & 0xFF);
                                byte g = (byte)((pixel >> 8) & 0xFF);
                                byte b = (byte)(pixel & 0xFF);
                                
                                // Apply decay to each component
                                a = (byte)(a * PHOSPHOR_DECAY);
                                r = (byte)(r * PHOSPHOR_DECAY);
                                g = (byte)(g * PHOSPHOR_DECAY);
                                b = (byte)(b * PHOSPHOR_DECAY);
                                
                                // Threshold to prevent infinite dim pixels
                                if (a < 8) a = 0;
                                if (r < 8) r = 0;
                                if (g < 8) g = 0;
                                if (b < 8) b = 0;
                                
                                persistenceBuffer[i] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                            }
                            // Start with decayed persistence buffer
                            pixelBuffer[i] = persistenceBuffer[i];
                        }
                    }
                    else
                    {
                        // Clear pixel buffer (fully transparent - alpha = 0)
                        Array.Clear(pixelBuffer, 0, pixelBuffer.Length);
                    }

                    int ptr = 0;
                    int scaledWidth = SCREEN_WIDTH * SCREEN_MULTIPLIER;
                    int scaledHeight = SCREEN_HEIGHT * SCREEN_MULTIPLIER;
                    for (int x = 0; x < scaledWidth; x += SCREEN_MULTIPLIER)
                    {
                        for (int y = scaledHeight; y > 0; y -= 8 * SCREEN_MULTIPLIER)
                        {
                            byte value = cpu.Video[ptr++];
                            for (int b = 0; b < 8; b++)
                            {
                                if ((value & (1 << b)) != 0)
                                {
                                    int pixelY = y - (b * SCREEN_MULTIPLIER);
                                    uint colorValue = GetColorValue(x, y);
                                    int bufferIndex = pixelY * scaledWidth + x;
                                    if (bufferIndex >= 0 && bufferIndex < pixelBuffer.Length)
                                    {
                                        for (int dy = 0; dy < SCREEN_MULTIPLIER; dy++)
                                        {
                                            if (pixelY + dy < scaledHeight)
                                            {
                                                for (int dx = 0; dx < SCREEN_MULTIPLIER; dx++)
                                                {
                                                    pixelBuffer[bufferIndex + (dy * scaledWidth) + dx] = colorValue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Store current frame for next frame's persistence effect
                    if (phosphorPersistenceEnabled && crtEffectEnabled)
                    {
                        Array.Copy(pixelBuffer, persistenceBuffer, pixelBuffer.Length);
                    }
                    
                    // Apply CRT post-processing effects
                    if (crtEffectEnabled)
                    {
                        ApplyCrtPostProcessing(scaledWidth, scaledHeight);
                    }

                    // Update texture with pixel buffer
                    unsafe
                    {
                        fixed (uint* pixels = pixelBuffer)
                        {
                            SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = scaledWidth, h = scaledHeight };
                            SDL.SDL_UpdateTexture(texture, ref fullRect, (IntPtr)pixels, scaledWidth * sizeof(uint));
                        }
                    }

                    // Clear renderer to black first
                    SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                    SDL.SDL_RenderClear(renderer);
                    
                    // Render background texture if enabled
                    if (backgroundEnabled && backgroundTexture != IntPtr.Zero)
                    {
                        SDL.SDL_RenderCopy(renderer, backgroundTexture, IntPtr.Zero, IntPtr.Zero);
                    }
                    
                    // Render game texture on top (with alpha blending - transparent pixels show background)
                    SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
                    
                    if (crtEffectEnabled)
                    {
                        // Draw CRT scanlines for authentic appearance
                        SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                        
                        // Vertical scanlines (due to rotated CRT)
                        SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 35);
                        for (int x = 0; x < scaledWidth; x += SCREEN_MULTIPLIER)
                        {
                            SDL.SDL_RenderDrawLine(renderer, x, 0, x, scaledHeight);
                        }
                        
                        // Horizontal scanlines (typical CRT raster lines)
                        SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 25);
                        for (int y = 0; y < scaledHeight; y += SCREEN_MULTIPLIER)
                        {
                            SDL.SDL_RenderDrawLine(renderer, 0, y, scaledWidth, y);
                        }
                        
                        // Apply vignette overlay for edge darkening
                        if (vignetteTexture != IntPtr.Zero)
                        {
                            SDL.SDL_RenderCopy(renderer, vignetteTexture, IntPtr.Zero, IntPtr.Zero);
                        }
                        
                        // Apply CRT screen mask with rounded corners
                        if (screenMaskTexture != IntPtr.Zero)
                        {
                            SDL.SDL_RenderCopy(renderer, screenMaskTexture, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    
                    // Draw overlay message if active
                    if (overlayMessage != null && DateTime.Now < overlayMessageEndTime)
                    {
                        DrawOverlayMessage(scaledWidth, scaledHeight);
                    }
                    else if (overlayMessage != null)
                    {
                        overlayMessage = null;
                    }
                    
                    SDL.SDL_RenderPresent(renderer);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Applies CRT post-processing effects: horizontal blur, flicker, jitter, and warmup.
        /// </summary>
        private void ApplyCrtPostProcessing(int width, int height)
        {
            // Calculate warmup brightness (0.0 to 1.0 over WARMUP_DURATION seconds)
            float elapsedSeconds = (float)(DateTime.Now - startupTime).TotalSeconds;
            float warmupFactor = Math.Min(1.0f, elapsedSeconds / WARMUP_DURATION);
            // Ease-in curve for more realistic tube warmup
            warmupFactor = warmupFactor * warmupFactor;
            
            // Calculate flicker (random brightness variation)
            float flickerFactor = 1.0f - (float)(crtRandom.NextDouble() * FLICKER_INTENSITY);
            
            // Combined brightness factor
            float brightnessFactor = warmupFactor * flickerFactor;
            
            // Determine if this frame has horizontal jitter
            int jitterOffset = 0;
            if (crtRandom.NextDouble() < JITTER_PROBABILITY)
            {
                jitterOffset = crtRandom.Next(-JITTER_MAX_PIXELS, JITTER_MAX_PIXELS + 1) * SCREEN_MULTIPLIER;
            }
            
            // Apply effects to pixel buffer
            uint[] tempBuffer = new uint[pixelBuffer.Length];
            
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
                    uint pixel = pixelBuffer[jitteredIndex];
                    
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
                    if (BLUR_STRENGTH > 0 && x > 0 && x < width - 1)
                    {
                        uint leftPixel = pixelBuffer[jitteredIndex - 1];
                        uint rightPixel = pixelBuffer[jitteredIndex + 1];
                        
                        if (leftPixel != 0 || rightPixel != 0)
                        {
                            byte lr = (byte)((leftPixel >> 16) & 0xFF);
                            byte lg = (byte)((leftPixel >> 8) & 0xFF);
                            byte lb = (byte)(leftPixel & 0xFF);
                            byte rr = (byte)((rightPixel >> 16) & 0xFF);
                            byte rg = (byte)((rightPixel >> 8) & 0xFF);
                            byte rb = (byte)(rightPixel & 0xFF);
                            
                            float centerWeight = 1.0f - BLUR_STRENGTH;
                            float sideWeight = BLUR_STRENGTH * 0.5f;
                            
                            r = (byte)(r * centerWeight + (lr + rr) * sideWeight);
                            g = (byte)(g * centerWeight + (lg + rg) * sideWeight);
                            b = (byte)(b * centerWeight + (lb + rb) * sideWeight);
                        }
                    }
                    
                    // Apply brightness (warmup + flicker)
                    r = (byte)(r * brightnessFactor);
                    g = (byte)(g * brightnessFactor);
                    b = (byte)(b * brightnessFactor);
                    a = (byte)(a * brightnessFactor);
                    
                    tempBuffer[srcIndex] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                }
            }
            
            // Copy back to pixel buffer
            Array.Copy(tempBuffer, pixelBuffer, pixelBuffer.Length);
        }

        private uint GetColorValue(int screenPos_X, int screenPos_Y)
        {
            // Convert SDL_Color to ARGB8888 format (0xAARRGGBB)
            // Base values are for 1x resolution, scaled by SCREEN_MULTIPLIER
            SDL.SDL_Color color;
            if (screenPos_Y < 239 * SCREEN_MULTIPLIER && screenPos_Y > 195 * SCREEN_MULTIPLIER)
                color = greenColor;
            else if (screenPos_Y < 256 * SCREEN_MULTIPLIER && screenPos_Y > 240 * SCREEN_MULTIPLIER && screenPos_X > 0 && screenPos_X < 127 * SCREEN_MULTIPLIER)
                color = greenColor;
            else if (screenPos_Y < 256 * SCREEN_MULTIPLIER && screenPos_Y > 240 * SCREEN_MULTIPLIER)
                color = whiteColor2;
            else if (screenPos_Y < 64 * SCREEN_MULTIPLIER && screenPos_Y > 32 * SCREEN_MULTIPLIER)
                color = redColor;
            else
                color = whiteColor;
            
            return ((uint)color.a << 24) | ((uint)color.r << 16) | ((uint)color.g << 8) | color.b;
        }

        private void HandleKeyDown(SDL.SDL_Keycode key)
        {
            if (key == SDL.SDL_Keycode.SDLK_ESCAPE)
            {
                Console.WriteLine("\nEscape pressed. Exiting...");
                CancellationTokenSource.Cancel();
                cpu?.Stop();
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_LEFTBRACKET)
            {
                ResizeDisplay(SCREEN_MULTIPLIER - 1);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_RIGHTBRACKET)
            {
                ResizeDisplay(SCREEN_MULTIPLIER + 1);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_b)
            {
                backgroundEnabled = !backgroundEnabled;
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_r)
            {
                crtEffectEnabled = !crtEffectEnabled;
                phosphorPersistenceEnabled = crtEffectEnabled;
                if (!phosphorPersistenceEnabled)
                {
                    // Clear persistence buffer when disabling
                    Array.Clear(persistenceBuffer, 0, persistenceBuffer.Length);
                }
                overlayMessage = crtEffectEnabled ? "crt:on" : "crt:off";
                overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_p)
            {
                gamePaused = !gamePaused;
                if (cpu != null) cpu.Paused = gamePaused;
                overlayMessage = gamePaused ? "paused" : null;
                overlayMessageEndTime = gamePaused ? DateTime.MaxValue : DateTime.Now;
                return;
            }
            
            
            if (key == SDL.SDL_Keycode.SDLK_s)
            {
                soundEnabled = !soundEnabled;
                overlayMessage = soundEnabled ? "sound:on" : "sound:off";
                overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            // DIP Switch controls (F1-F3)
            if (key == SDL.SDL_Keycode.SDLK_F1)
            {
                settings.CycleLives();
                ApplyDipSwitches();
                settings.Save();
                overlayMessage = $"lives:{settings.ActualLives}";
                overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F2)
            {
                settings.ToggleBonusLife();
                ApplyDipSwitches();
                settings.Save();
                overlayMessage = $"bonus:{settings.BonusLifeThreshold}";
                overlayMessageEndTime = DateTime.Now.AddSeconds(2);
                return;
            }
            
            if (key == SDL.SDL_Keycode.SDLK_F3)
            {
                settings.ToggleCoinInfo();
                ApplyDipSwitches();
                settings.Save();
                overlayMessage = settings.CoinInfoHidden ? "coininfo:off" : "coininfo:on";
                overlayMessageEndTime = DateTime.Now.AddSeconds(2);
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

            while (!soundLoop.IsCancellationRequested)
            {
                cpu!.SoundTiming.WaitOne();
                if (soundEnabled && prevPort3 != cpu!.PortOut[3])
                {
                    if (((cpu.PortOut[3] & 0x01) == 0x01) && ((cpu.PortOut[3] & 0x01) != (prevPort3 & 0x01)))
                        AudioPlaybackEngine.Instance.PlaySound(ufo_lowpitch);
                    if (((cpu.PortOut[3] & 0x02) == 0x02) && ((cpu.PortOut[3] & 0x02) != (prevPort3 & 0x02)))
                        AudioPlaybackEngine.Instance.PlaySound(shoot);
                    if (((cpu.PortOut[3] & 0x04) == 0x04) && ((cpu.PortOut[3] & 0x04) != (prevPort3 & 0x04)))
                        AudioPlaybackEngine.Instance.PlaySound(explosion);
                    if (((cpu.PortOut[3] & 0x08) == 0x08) && ((cpu.PortOut[3] & 0x08) != (prevPort3 & 0x08)))
                        AudioPlaybackEngine.Instance.PlaySound(invaderkilled);
                    if (((cpu.PortOut[3] & 0x08) == 0x08) && ((cpu.PortOut[3] & 0x10) != (prevPort3 & 0x10)))
                        AudioPlaybackEngine.Instance.PlaySound(extendedplay);
                }
                prevPort3 = cpu!.PortOut[3];

                if (soundEnabled && prevPort5 != cpu.PortOut[5])
                {
                    if (((cpu.PortOut[5] & 0x01) == 0x01) && ((cpu.PortOut[5] & 0x01) != (prevPort5 & 0x01)))
                        AudioPlaybackEngine.Instance.PlaySound(fastinvader1);
                    if (((cpu.PortOut[5] & 0x02) == 0x02) && ((cpu.PortOut[5] & 0x02) != (prevPort5 & 0x02)))
                        AudioPlaybackEngine.Instance.PlaySound(fastinvader2);
                    if (((cpu.PortOut[5] & 0x04) == 0x04) && ((cpu.PortOut[5] & 0x04) != (prevPort5 & 0x04)))
                        AudioPlaybackEngine.Instance.PlaySound(fastinvader3);
                    if (((cpu.PortOut[5] & 0x08) == 0x08) && ((cpu.PortOut[5] & 0x08) != (prevPort5 & 0x08)))
                        AudioPlaybackEngine.Instance.PlaySound(fastinvader4);
                    if (((cpu.PortOut[5] & 0x10) == 0x10) && ((cpu.PortOut[5] & 0x10) != (prevPort5 & 0x10)))
                        AudioPlaybackEngine.Instance.PlaySound(explosion);
                }
                prevPort5 = cpu!.PortOut[5];
                Thread.Sleep(4);
            }
       }

        private void DrawOverlayMessage(int screenWidth, int screenHeight)
        {
            if (overlayMessage == null) return;
            
            // Character dimensions (scaled)
            int charWidth = 5 * SCREEN_MULTIPLIER;
            int charHeight = 7 * SCREEN_MULTIPLIER;
            int charSpacing = 1 * SCREEN_MULTIPLIER;
            int totalWidth = overlayMessage.Length * (charWidth + charSpacing) - charSpacing;
            
            // Center position
            int startX = (screenWidth - totalWidth) / 2;
            int startY = (screenHeight - charHeight) / 2;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 180);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 10 * SCREEN_MULTIPLIER,
                y = startY - 5 * SCREEN_MULTIPLIER,
                w = totalWidth + 20 * SCREEN_MULTIPLIER,
                h = charHeight + 10 * SCREEN_MULTIPLIER
            };
            SDL.SDL_RenderFillRect(renderer, ref bgRect);
            
            // Draw each character
            SDL.SDL_SetRenderDrawColor(renderer, 0xFF, 0xFF, 0x00, 0xFF); // Yellow text
            for (int i = 0; i < overlayMessage.Length; i++)
            {
                int charX = startX + i * (charWidth + charSpacing);
                DrawChar(overlayMessage[i], charX, startY, SCREEN_MULTIPLIER);
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
                        SDL.SDL_RenderFillRect(renderer, ref pixelRect);
                    }
                }
            }
        }

        private void KeyPressed(uint key)
        {
            switch (key)
            {
                case 1: // Coin
                    inputPorts[1] |= 0x01;
                    break;

                case 2: // 1P Start
                    inputPorts[1] |= 0x04;
                    break;

                case 3: // 2P start
                    inputPorts[1] |= 0x02;
                    break;

                case 4: // 1P Left
                    inputPorts[1] |= 0x20;
                    break;

                case 5: // 1P Right
                    inputPorts[1] |= 0x40;
                    break;

                case 6: // 1P Fire
                    inputPorts[1] |= 0x10;
                    break;

                case 7: // 2P Left
                    inputPorts[2] |= 0x20;
                    break;

                case 8: // 2P Right
                    inputPorts[2] |= 0x40;
                    break;

                case 9: // 2P Fire
                    inputPorts[2] |= 0x10;
                    break;

                case 10: // Easter Egg Part 1
                    inputPorts[1] += 0x72;
                    break;

                case 11: // Easter Egg Part 2
                    inputPorts[1] += 0x34;
                    break;

                case 12: // Tilt
                    inputPorts[2] += 0x04;
                    break;
            }
        }

        private void KeyLifted(uint key)
        {
            switch (key)
            {
                case 1: // Coin
                    inputPorts[1] &= 0xFE;
                    break;

                case 2: // 1P Start
                    inputPorts[1] &= 0xFB;
                    break;

                case 3: // 2P start
                    inputPorts[1] &= 0xFD;
                    break;

                case 4: // 1P Left
                    inputPorts[1] &= 0xDF;
                    break;

                case 5: // 1P Right
                    inputPorts[1] &= 0xBF;
                    break;

                case 6: // 1P Fire
                    inputPorts[1] &= 0xEF;
                    break;

                case 7: // 2P Left
                    inputPorts[2] &= 0xDF;
                    break;

                case 8: // 2P Right
                    inputPorts[2] &= 0xBF;
                    break;

                case 9: // 2P Fire
                    inputPorts[2] &= 0xEF;
                    break;

                case 10: // Easter Egg Part 1
                    inputPorts[1] &= 0x8D;
                    break;

                case 11: // Easter Egg Part 2
                    inputPorts[1] &= 0xCB;
                    break;

                case 12: // Tilt
                    inputPorts[2] &= 0xFB;
                    break;
            }
        }
    }
}