using Invaders.MAINBOARD;
using SDL2;
using static SDL2.SDL;

namespace Invaders
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
        private readonly int SCREEN_WIDTH = 223;
        private readonly int SCREEN_HEIGHT = 256;
        private int SCREEN_MULTIPLIER = 2;
        private readonly object resizeLock = new();
        
        private IntPtr window;
        private IntPtr renderer;
        private IntPtr texture;
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
            pixelBuffer = new uint[(SCREEN_WIDTH * SCREEN_MULTIPLIER) * (SCREEN_HEIGHT * SCREEN_MULTIPLIER)];
            InitializeSDL();
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
                
                // Resize window
                SDL.SDL_SetWindowSize(window, SCREEN_WIDTH * SCREEN_MULTIPLIER, SCREEN_HEIGHT * SCREEN_MULTIPLIER);
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
        }

        public void Start()
        {
            ExecuteSpaceInvaders();
            
            // Monitor for SDL events
            Console.WriteLine("Controls: C=Coin, 1=1P Start, 2=2P Start, Arrows=Move, Space=Fire, ESC=Exit");
            Console.WriteLine("Scale: [=Decrease (1x-4x), ]=Increase (1x-4x), Default=2x");
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
                cpu!.DisplayTiming.WaitOne();
                lock (resizeLock)
                {
                    try
                    {
                    // Clear pixel buffer (black background)
                    Array.Clear(pixelBuffer, 0, pixelBuffer.Length);

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

                    // Update texture with pixel buffer
                    unsafe
                    {
                        fixed (uint* pixels = pixelBuffer)
                        {
                            SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = scaledWidth, h = scaledHeight };
                            SDL.SDL_UpdateTexture(texture, ref fullRect, (IntPtr)pixels, scaledWidth * sizeof(uint));
                        }
                    }

                    // Render texture to screen
                    SDL.SDL_RenderClear(renderer);
                    SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
                    
                    // Draw CRT scanlines for authentic appearance
                    SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                    SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 90); // Semi-transparent black
                    for (int y = 0; y < scaledHeight; y += SCREEN_MULTIPLIER)
                    {
                        SDL.SDL_RenderDrawLine(renderer, 0, y, scaledWidth, y);
                    }
                    
                    SDL.SDL_RenderPresent(renderer);
                    }
                    catch { }
                }
            }
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
                SDL.SDL_Keycode.SDLK_o => 10,     // Easter Egg Part 1
                SDL.SDL_Keycode.SDLK_p => 11,     // Easter Egg Part 2
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
                if (prevPort3 != cpu!.PortOut[3])
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
                    prevPort3 = cpu!.PortOut[3];
                }

                if (prevPort5 != cpu.PortOut[5])
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
                    prevPort5 = cpu!.PortOut[5];
                }
                Thread.Sleep(4);
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