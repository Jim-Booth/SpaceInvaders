// ============================================================================
// Project:     SpaceInvaders
// File:        CrtEffects.cs
// Description: CRT display effects including phosphor persistence, scanlines,
//              bloom/glow, vignette, screen curvature, flicker, and jitter
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using SDL2;

namespace SpaceInvaders.CABINET
{
    /// <summary>
    /// Manages CRT visual effects for authentic arcade cabinet appearance.
    /// </summary>
    public class CrtEffects : IDisposable
    {
        // CRT curvature settings (BarrelDistortion reserved for future use)
        private readonly float CornerRadius = 0.08f;
        
        // Phosphor persistence settings
        private readonly float PhosphorDecay = 0.75f;
        private uint[] _persistenceBuffer = [];
        
        // Post-processing effect settings
        private readonly float FlickerIntensity = 0.02f;
        private readonly float JitterProbability = 0.002f;
        private readonly int JitterMaxPixels = 1;
        private readonly float WarmupDuration = 2.0f; // seconds
        private readonly byte BloomAlpha = 40;
        
        // CRT power-on bounce settings (simulates magnetic coil energizing)
        private readonly float BounceOvershoot = 0.80f;      // Brightness overshoot (0.80 = 80% over)
        private readonly float BounceDamping = 3.0f;         // How quickly oscillation decays
        private readonly float BounceFrequency = 3.0f;       // Number of oscillations during settle
        private readonly int BounceMaxPixels = 30;           // Maximum pixel offset during bounce
        private readonly float PositionBounceDamping = 5.0f; // How quickly position settles 
        private readonly float PositionBounceFrequency = 8.0f; // Position oscillation frequency
        
        // CRT power-off animation settings (classic CRT shutdown effect)
        private readonly float PowerOffHorizontalDuration = 0.15f; // Seconds to shrink width to vertical line
        private readonly float PowerOffVerticalDuration = 0.20f;   // Seconds to shrink line to dot
        private readonly float PowerOffDotDuration = 0.10f;        // Seconds for dot to fade out        
        private readonly Random _random = new();
        private readonly DateTime _startupTime;
        
        /// <summary>
        /// Gets whether the CRT warmup period has completed (brightness at full).
        /// </summary>
        public bool WarmupComplete => (DateTime.Now - _startupTime).TotalSeconds >= WarmupDuration + 1.0f;
        
        /// <summary>
        /// Gets the current horizontal screen offset for the bounce effect.
        /// Returns pixel offset to apply to the render destination rectangle.
        /// The original Space Invaders CRT is rotated 90�, so vertical deflection
        /// coil bounce appears as horizontal movement to the player.
        /// </summary>
        public int GetScreenBounceOffset(int screenMultiplier)
        {
            if (!Enabled) return 0;
            
            float elapsedSeconds = (float)(DateTime.Now - _startupTime).TotalSeconds;
            float t = elapsedSeconds / WarmupDuration;
            
            if (t < 0.3f)
            {
                // First 30% of warmup: image starts offset and quickly moves to center
                float settleProgress = t / 0.3f; // 0 to 1 over first 30%
                float startOffset = 1.0f - (settleProgress * settleProgress * settleProgress); // Cubic ease-out
                return (int)(BounceMaxPixels * startOffset * screenMultiplier * 0.5f);
            }
            
            // After initial settle (30% onwards): damped oscillation bounce
            float settleTime = (t - 0.3f) * WarmupDuration; // Time since settling completed
            float decay = MathF.Exp(-PositionBounceDamping * settleTime);
            float oscillation = MathF.Sin(settleTime * MathF.PI * PositionBounceFrequency * 2f);
            float bounce = decay * oscillation;
            
            return (int)(BounceMaxPixels * bounce * screenMultiplier);
        }
        
        // SDL textures for overlay effects
        private IntPtr _vignetteTexture;
        private IntPtr _screenMaskTexture;
        private IntPtr _scanlinesTexture;
        private IntPtr _shadowMaskTexture;
        private IntPtr _bloomTexture;
        private IntPtr _renderer;
        
        // Bloom buffer for GPU-based bloom effect
        private uint[] _bloomBuffer = [];
        
        private int _width;
        private int _height;
        private int _screenMultiplier;
        
        public bool Enabled { get; set; } = true;

        public CrtEffects(IntPtr renderer, int screenWidth, int screenHeight, int screenMultiplier)
        {
            _renderer = renderer;
            _screenMultiplier = screenMultiplier;
            _width = screenWidth * screenMultiplier;
            _height = screenHeight * screenMultiplier;
            _startupTime = DateTime.Now;
            _persistenceBuffer = new uint[_width * _height];
            _bloomBuffer = new uint[_width * _height];
            
            CreateVignetteTexture();
            CreateScreenMaskTexture();
            CreateScanlinesTexture();
            CreateShadowMaskTexture();
            CreateBloomTexture();
        }

        /// <summary>
        /// Recreates textures and buffers for a new screen size.
        /// </summary>
        public void Resize(int screenWidth, int screenHeight, int screenMultiplier)
        {
            _screenMultiplier = screenMultiplier;
            _width = screenWidth * screenMultiplier;
            _height = screenHeight * screenMultiplier;
            _persistenceBuffer = new uint[_width * _height];
            _bloomBuffer = new uint[_width * _height];
            
            CreateVignetteTexture();
            CreateScreenMaskTexture();
            CreateScanlinesTexture();
            CreateShadowMaskTexture();
            CreateBloomTexture();
        }

        /// <summary>
        /// Applies phosphor persistence effect - decays previous frame and blends with current.
        /// Call before rendering new frame pixels.
        /// </summary>
        public void ApplyPersistence(uint[] pixelBuffer)
        {
            if (!Enabled)
            {
                Array.Clear(pixelBuffer, 0, pixelBuffer.Length);
                return;
            }
            
            for (int i = 0; i < _persistenceBuffer.Length; i++)
            {
                uint pixel = _persistenceBuffer[i];
                if (pixel != 0)
                {
                    byte a = (byte)((pixel >> 24) & 0xFF);
                    byte r = (byte)((pixel >> 16) & 0xFF);
                    byte g = (byte)((pixel >> 8) & 0xFF);
                    byte b = (byte)(pixel & 0xFF);
                    
                    a = (byte)(a * PhosphorDecay);
                    r = (byte)(r * PhosphorDecay);
                    g = (byte)(g * PhosphorDecay);
                    b = (byte)(b * PhosphorDecay);
                    
                    if (a < 8) a = 0;
                    if (r < 8) r = 0;
                    if (g < 8) g = 0;
                    if (b < 8) b = 0;
                    
                    _persistenceBuffer[i] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                }
                pixelBuffer[i] = _persistenceBuffer[i];
            }
        }

        /// <summary>
        /// Stores current frame for next frame's persistence effect.
        /// Call after rendering frame pixels.
        /// </summary>
        public void StorePersistence(uint[] pixelBuffer)
        {
            if (Enabled)
            {
                Array.Copy(pixelBuffer, _persistenceBuffer, pixelBuffer.Length);
            }
        }

        /// <summary>
        /// Clears the persistence buffer (used when disabling effects).
        /// </summary>
        public void ClearPersistence()
        {
            Array.Clear(_persistenceBuffer, 0, _persistenceBuffer.Length);
        }

        /// <summary>
        /// Clears the pixel buffer with a memory access pattern similar to ApplyPersistence.
        /// This maintains consistent CPU cache/prefetch behavior regardless of CRT state,
        /// avoiding performance drops when switching CRT effects off.
        /// </summary>
        public void ClearPixelBuffer(uint[] pixelBuffer)
        {
            // Use a loop that matches ApplyPersistence memory access pattern
            // This keeps CPU frequency and cache behavior consistent
            for (int i = 0; i < _persistenceBuffer.Length && i < pixelBuffer.Length; i++)
            {
                // Read from persistence buffer (maintains read pattern)
                // but always write zero to pixel buffer
                _ = _persistenceBuffer[i];
                pixelBuffer[i] = 0;
            }
        }

        /// <summary>
        /// Applies post-processing effects: flicker, jitter, warmup, and prepares bloom for GPU.
        /// Bloom and blur are now GPU-accelerated via SDL texture blending.
        /// </summary>
        public void ApplyPostProcessing(uint[] pixelBuffer, int width, int height)
        {
            if (!Enabled) return;
            
            // Prepare bloom data for GPU rendering (extract bright pixels)
            PrepareBloomData(pixelBuffer, width, height);
            
            float elapsedSeconds = (float)(DateTime.Now - _startupTime).TotalSeconds;
            float warmupFactor = CalculateWarmupFactor(elapsedSeconds);
            
            float flickerFactor = 1.0f - (float)(_random.NextDouble() * FlickerIntensity);
            float brightnessFactor = warmupFactor * flickerFactor;
            
            int jitterOffset = 0;
            if (_random.NextDouble() < JitterProbability)
            {
                jitterOffset = _random.Next(-JitterMaxPixels, JitterMaxPixels + 1) * _screenMultiplier;
            }
            
            // Apply jitter and brightness adjustments (including bounce overshoot > 1.0)
            if (jitterOffset != 0 || brightnessFactor < 0.99f || brightnessFactor > 1.01f)
            {
                uint[] tempBuffer = new uint[pixelBuffer.Length];
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIndex = y * width + x;
                        
                        int jitteredX = x + jitterOffset;
                        if (jitteredX < 0 || jitteredX >= width)
                        {
                            tempBuffer[srcIndex] = 0;
                            continue;
                        }
                        
                        int jitteredIndex = y * width + jitteredX;
                        uint pixel = pixelBuffer[jitteredIndex];
                        
                        if (pixel == 0)
                        {
                            tempBuffer[srcIndex] = 0;
                            continue;
                        }
                        
                        byte a = (byte)((pixel >> 24) & 0xFF);
                        byte r = (byte)((pixel >> 16) & 0xFF);
                        byte g = (byte)((pixel >> 8) & 0xFF);
                        byte b = (byte)(pixel & 0xFF);
                        
                        // Apply brightness factor (warmup + flicker + bounce)
                        if (a > 100)
                        {
                            r = (byte)Math.Min(255, (int)(r * brightnessFactor));
                            g = (byte)Math.Min(255, (int)(g * brightnessFactor));
                            b = (byte)Math.Min(255, (int)(b * brightnessFactor));
                            a = (byte)Math.Min(255, (int)(a * brightnessFactor));
                        }
                        
                        tempBuffer[srcIndex] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                    }
                }
                
                Array.Copy(tempBuffer, pixelBuffer, pixelBuffer.Length);
            }
        }
        
        /// <summary>
        /// Calculates the warmup brightness factor with power-on bounce effect.
        /// Simulates CRT magnetic coils energizing - overshoots then settles.
        /// </summary>
        private float CalculateWarmupFactor(float elapsedSeconds)
        {
            float t = elapsedSeconds / WarmupDuration;
            
            if (t >= 1.0f)
            {
                // Warmup complete - apply bounce effect
                float settleTime = (elapsedSeconds - WarmupDuration);
                
                // Use absolute value of damped sine for distinct "pulses" that decay
                // This creates: bright -> dim -> bright -> dim -> settle pattern
                float settleDecay = MathF.Exp(-BounceDamping * settleTime);
                float bounce = settleDecay * MathF.Sin(settleTime * MathF.PI * BounceFrequency * 2f);
                
                // Scale bounce effect: positive = brighter, negative = dimmer
                float result = 1.0f + (BounceOvershoot * bounce);
                return Math.Max(0.3f, result); // Don't let it go too dark
            }
            
            // During warmup: ramp up and overshoot at the end
            // Use smooth step that overshoots target
            float warmupFactor;
            if (t < 0.7f)
            {
                // First 70%: smooth ramp from 0 toward 1
                float normalizedT = t / 0.7f;
                warmupFactor = normalizedT * normalizedT * (3f - 2f * normalizedT); // Smooth step
            }
            else
            {
                // Last 30%: accelerate past 1.0 to create overshoot at t=1.0
                float overshootT = (t - 0.7f) / 0.3f; // 0 to 1 over last 30%
                float baseLevel = 1.0f; // We've reached full brightness
                float overshoot = overshootT * BounceOvershoot; // Ramp up overshoot
                warmupFactor = baseLevel + overshoot;
            }
            
            return warmupFactor;
        }
        
        /// <summary>
        /// Prepares bloom data by extracting bright pixels for GPU-accelerated rendering.
        /// Fills complete pixel blocks to avoid stippling artifacts.
        /// </summary>
        private void PrepareBloomData(uint[] pixelBuffer, int width, int height)
        {
            Array.Clear(_bloomBuffer, 0, _bloomBuffer.Length);
            
            int step = _screenMultiplier;
            
            // Sample at pixel boundaries but fill complete blocks
            for (int y = 0; y < height; y += step)
            {
                for (int x = 0; x < width; x += step)
                {
                    int index = y * width + x;
                    if (index >= pixelBuffer.Length) continue;
                    
                    uint pixel = pixelBuffer[index];
                    if (pixel == 0) continue;
                    
                    byte r = (byte)((pixel >> 16) & 0xFF);
                    byte g = (byte)((pixel >> 8) & 0xFF);
                    byte b = (byte)(pixel & 0xFF);
                    
                    // Only bloom bright pixels
                    float brightness = Math.Max(r, Math.Max(g, b)) / 255.0f;
                    if (brightness < 0.5f) continue;
                    
                    uint bloomColor = ((uint)BloomAlpha << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                    
                    // Fill the entire pixel block to avoid stippling
                    for (int dy = 0; dy < step && (y + dy) < height; dy++)
                    {
                        for (int dx = 0; dx < step && (x + dx) < width; dx++)
                        {
                            int fillIndex = (y + dy) * width + (x + dx);
                            if (fillIndex < _bloomBuffer.Length)
                            {
                                _bloomBuffer[fillIndex] = bloomColor;
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates the bloom texture with current frame's bloom data.
        /// Call before RenderOverlays.
        /// </summary>
        public void UpdateBloomTexture()
        {
            if (!Enabled || _bloomTexture == IntPtr.Zero) return;
            
            unsafe
            {
                fixed (uint* pixels = _bloomBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = _width, h = _height };
                    SDL.SDL_UpdateTexture(_bloomTexture, ref fullRect, (IntPtr)pixels, _width * sizeof(uint));
                }
            }
        }

        /// <summary>
        /// Renders GPU-accelerated bloom and overlay textures (scanlines, vignette, screen mask).
        /// Call after rendering the game texture.
        /// </summary>
        public void RenderOverlays(IntPtr renderer, int width, int height, int screenMultiplier)
        {
            if (!Enabled) return;
            
            // Render GPU-accelerated bloom with additive blending (glow effect)
            if (_bloomTexture != IntPtr.Zero)
            {
                // Update bloom texture with current frame data
                UpdateBloomTexture();
                
                // Render bloom with minimal offset passes for a subtle glow
                int blurRadius = Math.Max(1, screenMultiplier / 2);
                
                // Center pass
                SDL.SDL_RenderCopy(renderer, _bloomTexture, IntPtr.Zero, IntPtr.Zero);
                
                // Horizontal offset passes only (subtle horizontal glow)
                SDL.SDL_Rect leftRect = new SDL.SDL_Rect { x = -blurRadius, y = 0, w = width, h = height };
                SDL.SDL_Rect rightRect = new SDL.SDL_Rect { x = blurRadius, y = 0, w = width, h = height };
                SDL.SDL_RenderCopy(renderer, _bloomTexture, IntPtr.Zero, ref leftRect);
                SDL.SDL_RenderCopy(renderer, _bloomTexture, IntPtr.Zero, ref rightRect);
            }
            
            // Render pre-generated scanlines texture
            if (_scanlinesTexture != IntPtr.Zero)
            {
                SDL.SDL_RenderCopy(renderer, _scanlinesTexture, IntPtr.Zero, IntPtr.Zero);
            }
            
            // Shadow mask (RGB phosphor pattern)
            if (_shadowMaskTexture != IntPtr.Zero)
            {
                SDL.SDL_RenderCopy(renderer, _shadowMaskTexture, IntPtr.Zero, IntPtr.Zero);
            }
            
            // Vignette overlay
            if (_vignetteTexture != IntPtr.Zero)
            {
                SDL.SDL_RenderCopy(renderer, _vignetteTexture, IntPtr.Zero, IntPtr.Zero);
            }
            
            // Screen mask with rounded corners
            if (_screenMaskTexture != IntPtr.Zero)
            {
                SDL.SDL_RenderCopy(renderer, _screenMaskTexture, IntPtr.Zero, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Creates the bloom texture for GPU-accelerated glow effects.
        /// Uses additive blending to create light bloom around bright pixels.
        /// </summary>
        private void CreateBloomTexture()
        {
            if (_bloomTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_bloomTexture);
            
            _bloomTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                _width,
                _height
            );
            
            if (_bloomTexture == IntPtr.Zero)
                return;
            
            // Use additive blending for bloom glow effect
            SDL.SDL_SetTextureBlendMode(_bloomTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_ADD);
        }

        private void CreateVignetteTexture()
        {
            if (_vignetteTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_vignetteTexture);
            
            _vignetteTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                _width,
                _height
            );
            
            if (_vignetteTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(_vignetteTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            uint[] vignetteBuffer = new uint[_width * _height];
            float centerX = _width / 2.0f;
            float centerY = _height / 2.0f;
            float maxDist = (float)Math.Sqrt(centerX * centerX + centerY * centerY);
            
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float normalizedDist = dist / maxDist;
                    
                    float vignette = normalizedDist * normalizedDist * 0.6f;
                    byte alpha = (byte)(Math.Min(vignette * 255, 180));
                    
                    vignetteBuffer[y * _width + x] = ((uint)alpha << 24) | 0x000000;
                }
            }
            
            unsafe
            {
                fixed (uint* pixels = vignetteBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = _width, h = _height };
                    SDL.SDL_UpdateTexture(_vignetteTexture, ref fullRect, (IntPtr)pixels, _width * sizeof(uint));
                }
            }
        }

        private void CreateScreenMaskTexture()
        {
            if (_screenMaskTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_screenMaskTexture);
            
            _screenMaskTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                _width,
                _height
            );
            
            if (_screenMaskTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(_screenMaskTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            uint[] maskBuffer = new uint[_width * _height];
            float centerX = _width / 2.0f;
            float centerY = _height / 2.0f;
            float cornerRadius = Math.Min(_width, _height) * CornerRadius;
            
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    float nx = (x - centerX) / centerX;
                    float ny = (y - centerY) / centerY;
                    
                    float cornerX = Math.Max(0, Math.Abs(x - centerX) - (centerX - cornerRadius));
                    float cornerY = Math.Max(0, Math.Abs(y - centerY) - (centerY - cornerRadius));
                    float cornerDist = (float)Math.Sqrt(cornerX * cornerX + cornerY * cornerY);
                    
                    float r2 = nx * nx + ny * ny;
                    float edgeDark = r2 * 0.1f;
                    
                    byte alpha;
                    if (cornerDist > cornerRadius)
                    {
                        alpha = 255;
                    }
                    else if (cornerDist > cornerRadius - 2)
                    {
                        float t = (cornerDist - (cornerRadius - 2)) / 2.0f;
                        alpha = (byte)(t * 255);
                    }
                    else
                    {
                        alpha = (byte)(Math.Min(edgeDark * 60, 40));
                    }
                    
                    maskBuffer[y * _width + x] = ((uint)alpha << 24) | 0x000000;
                }
            }
            
            unsafe
            {
                fixed (uint* pixels = maskBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = _width, h = _height };
                    SDL.SDL_UpdateTexture(_screenMaskTexture, ref fullRect, (IntPtr)pixels, _width * sizeof(uint));
                }
            }
        }

        /// <summary>
        /// Creates the pre-rendered scanlines texture.
        /// Renders vertical scanlines only (horizontal scanlines removed as the original
        /// CRT is rotated 90�, making vertical lines the authentic scanline direction).
        /// </summary>
        private void CreateScanlinesTexture()
        {
            if (_scanlinesTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_scanlinesTexture);
            
            _scanlinesTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                _width,
                _height
            );
            
            if (_scanlinesTexture == IntPtr.Zero)
                return;
            
            SDL.SDL_SetTextureBlendMode(_scanlinesTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            uint[] scanlinesBuffer = new uint[_width * _height];
            
            // Vertical scanlines only (alpha = 35)
            uint verticalLineColor = (35u << 24) | 0x000000;
            
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if ((x % _screenMultiplier) == 0)
                    {
                        scanlinesBuffer[y * _width + x] = verticalLineColor;
                    }
                    // else: pixel remains 0 (fully transparent)
                }
            }
            
            unsafe
            {
                fixed (uint* pixels = scanlinesBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = _width, h = _height };
                    SDL.SDL_UpdateTexture(_scanlinesTexture, ref fullRect, (IntPtr)pixels, _width * sizeof(uint));
                }
            }
        }

        /// <summary>
        /// Creates the RGB shadow mask texture simulating CRT phosphor triads.
        /// This creates the characteristic RGB dot pattern visible on real CRT screens.
        /// </summary>
        private void CreateShadowMaskTexture()
        {
            if (_shadowMaskTexture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(_shadowMaskTexture);
            
            _shadowMaskTexture = SDL.SDL_CreateTexture(
                _renderer,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                _width,
                _height
            );
            
            if (_shadowMaskTexture == IntPtr.Zero)
                return;
            
            // Use multiplicative blending to darken non-phosphor areas
            SDL.SDL_SetTextureBlendMode(_shadowMaskTexture, SDL.SDL_BlendMode.SDL_BLENDMODE_MOD);
            
            uint[] maskBuffer = new uint[_width * _height];
            
            // Shadow mask pattern: RGB phosphor triads
            // Each "pixel" on a CRT is actually 3 colored phosphor dots
            // Pattern repeats every 3 columns horizontally
            int cellWidth = Math.Max(1, _screenMultiplier);
            int cellHeight = Math.Max(1, _screenMultiplier);
            
            // Phosphor brightness - very subtle effect (closer to 255 = less visible)
            byte phosphorBright = 255;
            byte phosphorDim = 250;  // Gap between phosphors barely visible
            
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    // Determine which phosphor this pixel belongs to (R, G, or B)
                    int cellX = x / cellWidth;
                    int phosphorIndex = cellX % 3;  // 0=R, 1=G, 2=B
                    
                    // Position within the cell
                    int subX = x % cellWidth;
                    int subY = y % cellHeight;
                    
                    // Create subtle gaps between phosphors (edges of cells are darker)
                    bool isEdge = (subX == 0) || (subY == 0 && cellHeight > 1);
                    byte intensity = isEdge ? phosphorDim : phosphorBright;
                    
                    // Apply RGB color based on phosphor position - very subtle
                    // For MOD blend mode: white (0xFFFFFF) = no change, darker = darkens that channel
                    byte r, g, b;
                    switch (phosphorIndex)
                    {
                        case 0: // Red phosphor - let red through, slightly dim green and blue
                            r = intensity;
                            g = (byte)(intensity * 0.95f);
                            b = (byte)(intensity * 0.95f);
                            break;
                        case 1: // Green phosphor - let green through, slightly dim red and blue
                            r = (byte)(intensity * 0.95f);
                            g = intensity;
                            b = (byte)(intensity * 0.95f);
                            break;
                        case 2: // Blue phosphor - let blue through, slightly dim red and green
                        default:
                            r = (byte)(intensity * 0.95f);
                            g = (byte)(intensity * 0.95f);
                            b = intensity;
                            break;
                    }
                    
                    maskBuffer[y * _width + x] = (0xFFu << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                }
            }
            
            unsafe
            {
                fixed (uint* pixels = maskBuffer)
                {
                    SDL.SDL_Rect fullRect = new SDL.SDL_Rect { x = 0, y = 0, w = _width, h = _height };
                    SDL.SDL_UpdateTexture(_shadowMaskTexture, ref fullRect, (IntPtr)pixels, _width * sizeof(uint));
                }
            }
        }

        public void Dispose()
        {
            if (_vignetteTexture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(_vignetteTexture);
                _vignetteTexture = IntPtr.Zero;
            }
            if (_screenMaskTexture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(_screenMaskTexture);
                _screenMaskTexture = IntPtr.Zero;
            }
            if (_scanlinesTexture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(_scanlinesTexture);
                _scanlinesTexture = IntPtr.Zero;
            }
            if (_bloomTexture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(_bloomTexture);
                _bloomTexture = IntPtr.Zero;
            }
        }
        
        /// <summary>
        /// Renders the CRT power-off animation sequence.
        /// Phase 1: Screen shrinks horizontally to a vertical line
        /// Phase 2: Vertical line shrinks to a bright dot
        /// Phase 3: Dot fades out
        /// </summary>
        /// <param name="renderer">SDL renderer</param>
        /// <param name="texture">Game texture to animate</param>
        /// <param name="width">Screen width</param>
        /// <param name="height">Screen height</param>
        public void RenderPowerOffAnimation(IntPtr renderer, IntPtr texture, int width, int height)
        {
            float totalDuration = PowerOffHorizontalDuration + PowerOffVerticalDuration + PowerOffDotDuration;
            var startTime = DateTime.Now;
            

            while (true)
            {
                float elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                if (elapsed >= totalDuration) break;
                
                // Clear to black
                SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL.SDL_RenderClear(renderer);
                
                int centerX = width / 2;
                int centerY = height / 2;
                
                if (elapsed < PowerOffHorizontalDuration)
                {
                    // Phase 1: Shrink width to a vertical line
                    float t = elapsed / PowerOffHorizontalDuration;
                    float easeT = t * t; // Ease-in (accelerate)
                    int currentWidth = (int)(width * (1.0f - easeT));
                    currentWidth = Math.Max(4, currentWidth); // Minimum 4 pixels wide
                    
                    SDL.SDL_Rect destRect = new SDL.SDL_Rect
                    {
                        x = centerX - currentWidth / 2,
                        y = 0,
                        w = currentWidth,
                        h = height
                    };
                    SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref destRect);
                }
                else if (elapsed < PowerOffHorizontalDuration + PowerOffVerticalDuration)
                {
                    // Phase 2: Shrink vertical line to a dot
                    float t = (elapsed - PowerOffHorizontalDuration) / PowerOffVerticalDuration;
                    float easeT = t * t; // Ease-in
                    int currentHeight = (int)(height * (1.0f - easeT));
                    currentHeight = Math.Max(4, currentHeight); // Minimum 4 pixels tall
                    int lineWidth = 4;
                    
                    // Draw bright white line/dot
                    SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                    SDL.SDL_Rect lineRect = new SDL.SDL_Rect
                    {
                        x = centerX - lineWidth / 2,
                        y = centerY - currentHeight / 2,
                        w = lineWidth,
                        h = currentHeight
                    };
                    SDL.SDL_RenderFillRect(renderer, ref lineRect);
                }
                else
                {
                    // Phase 3: Fade out the dot
                    float t = (elapsed - PowerOffHorizontalDuration - PowerOffVerticalDuration) / PowerOffDotDuration;
                    byte brightness = (byte)(255 * (1.0f - t));
                    int dotSize = 4;
                    
                    SDL.SDL_SetRenderDrawColor(renderer, brightness, brightness, brightness, 255);
                    SDL.SDL_Rect dotRect = new SDL.SDL_Rect
                    {
                        x = centerX - dotSize / 2,
                        y = centerY - dotSize / 2,
                        w = dotSize,
                        h = dotSize
                    };
                    SDL.SDL_RenderFillRect(renderer, ref dotRect);
                }
                
                SDL.SDL_RenderPresent(renderer);
                Thread.Sleep(16); // ~60 FPS
            }
            
            // Final black frame
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
            SDL.SDL_RenderClear(renderer);
            SDL.SDL_RenderPresent(renderer);
        }     
    }
}
