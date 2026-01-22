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
        private readonly int SCREEN_WIDTH = 446;
        private readonly int SCREEN_HEIGHT = 512;
        
        private IntPtr window;
        private IntPtr renderer;
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
            InitializeSDL();
        }

        private void InitializeSDL()
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                throw new Exception($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");
            }

            window = SDL.SDL_CreateWindow(
                "Space Invaders",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                SCREEN_WIDTH,
                SCREEN_HEIGHT,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN
            );

            if (window == IntPtr.Zero)
            {
                throw new Exception($"Window could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }

            renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (renderer == IntPtr.Zero)
            {
                throw new Exception($"Renderer could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }
        }

        public void Start()
        {
            ExecuteSpaceInvaders();
            
            // Monitor for SDL events
            Console.WriteLine("Controls: C=Coin, 1=1P Start, 2=2P Start, Arrows=Move, Space=Fire, ESC=Exit");
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
                
                Thread.Sleep(16); // ~60 FPS event polling
            }
            
            // Wait for threads to finish
            Console.WriteLine("Waiting for threads to terminate...");
            cpu_thread?.Join(2000);
            port_thread?.Join(1000);
            display_thread?.Join(1000);
            sound_thread?.Join(1000);
            AudioPlaybackEngine.Instance.Dispose();
            
            // Cleanup SDL
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

            cpu_thread = new Thread(() => cpu!.Start())
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

        private void PortThread()
        {
            while (!portLoop.IsCancellationRequested)
            {
                while (cpu!.PortIn == inputPorts)
                {
                    Thread.Sleep(4);
                }
                cpu.PortIn = inputPorts;
            }
        }

        public void DisplayThread()
        {
            while (!displayLoop.IsCancellationRequested)
            {
                cpu!.DisplayTiming.WaitOne();
                try
                {
                    // Clear the renderer
                    SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                    SDL.SDL_RenderClear(renderer);

                    int ptr = 0;
                    for (int x = 0; x < SCREEN_WIDTH; x += 2)
                    {
                        for (int y = SCREEN_HEIGHT; y > 0; y -= 16)
                        {
                            byte value = cpu.Video[ptr++];
                            for (int b = 0; b < 8; b++)
                            {
                                if ((value & (1 << b)) != 0)
                                {
                                    int pixelY = y - (b * 2);
                                    SDL.SDL_Color color = GetSDLColor(x, y);
                                    SDL.SDL_SetRenderDrawColor(renderer, color.r, color.g, color.b, color.a);
                                    
                                    SDL.SDL_Rect rect = new SDL.SDL_Rect
                                    {
                                        x = x,
                                        y = pixelY,
                                        w = 2,
                                        h = 2
                                    };
                                    SDL.SDL_RenderFillRect(renderer, ref rect);
                                }
                            }
                        }
                    }

                    SDL.SDL_RenderPresent(renderer);
                }
                catch { }
            }
        }

        private static SDL.SDL_Color GetSDLColor(int screenPos_X, int screenPos_Y)
        {
            if (screenPos_Y < 478 && screenPos_Y > 390) return greenColor;
            if (screenPos_Y < 512 && screenPos_Y > 480) return whiteColor2;
            if ((screenPos_Y < 512 && screenPos_Y > 480) && (screenPos_X > 0 && screenPos_X < 254)) return greenColor;
            if (screenPos_Y < 128 && screenPos_Y > 64) return redColor;
            return whiteColor;
        }

        private void HandleKeyDown(SDL.SDL_Keycode key)
        {
            uint keyValue = GetKeyValue(key);
            if (keyValue == 99) return; // Unknown key
            
            if (key == SDL.SDL_Keycode.SDLK_ESCAPE)
            {
                Console.WriteLine("\nEscape pressed. Exiting...");
                CancellationTokenSource.Cancel();
                cpu?.Stop();
                return;
            }
            
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