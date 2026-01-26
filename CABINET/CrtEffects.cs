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
        private readonly float WarmupDuration = 2.0f;
        private readonly byte BloomAlpha = 40;
        
        private readonly Random _random = new();
        private readonly DateTime _startupTime;
        
        // SDL textures for overlay effects
        private IntPtr _vignetteTexture;
        private IntPtr _screenMaskTexture;
        private IntPtr _scanlinesTexture;
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
        /// Applies post-processing effects: flicker, jitter, warmup, and prepares bloom for GPU.
        /// Bloom and blur are now GPU-accelerated via SDL texture blending.
        /// </summary>
        public void ApplyPostProcessing(uint[] pixelBuffer, int width, int height)
        {
            if (!Enabled) return;
            
            // Prepare bloom data for GPU rendering (extract bright pixels)
            PrepareBloomData(pixelBuffer, width, height);
            
            float elapsedSeconds = (float)(DateTime.Now - _startupTime).TotalSeconds;
            float warmupFactor = Math.Min(1.0f, elapsedSeconds / WarmupDuration);
            warmupFactor = warmupFactor * warmupFactor;
            
            float flickerFactor = 1.0f - (float)(_random.NextDouble() * FlickerIntensity);
            float brightnessFactor = warmupFactor * flickerFactor;
            
            int jitterOffset = 0;
            if (_random.NextDouble() < JitterProbability)
            {
                jitterOffset = _random.Next(-JitterMaxPixels, JitterMaxPixels + 1) * _screenMultiplier;
            }
            
            // Only apply jitter and brightness if needed (skip expensive per-pixel loop otherwise)
            if (jitterOffset != 0 || brightnessFactor < 0.99f)
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
                        
                        // Apply brightness factor (warmup + flicker)
                        if (a > 100)
                        {
                            r = (byte)(r * brightnessFactor);
                            g = (byte)(g * brightnessFactor);
                            b = (byte)(b * brightnessFactor);
                            a = (byte)(a * brightnessFactor);
                        }
                        
                        tempBuffer[srcIndex] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                    }
                }
                
                Array.Copy(tempBuffer, pixelBuffer, pixelBuffer.Length);
            }
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
                    float edgeDark = r2 * 0.3f;
                    
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
        /// This replaces ~479 individual SDL_RenderDrawLine calls with a single texture render.
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
            
            // Vertical scanlines (alpha = 35)
            uint verticalLineColor = (35u << 24) | 0x000000;
            // Horizontal scanlines (alpha = 25)
            uint horizontalLineColor = (25u << 24) | 0x000000;
            // Intersection points get combined alpha
            uint intersectionColor = (55u << 24) | 0x000000;
            
            for (int y = 0; y < _height; y++)
            {
                bool isHorizontalLine = (y % _screenMultiplier) == 0;
                
                for (int x = 0; x < _width; x++)
                {
                    bool isVerticalLine = (x % _screenMultiplier) == 0;
                    
                    if (isVerticalLine && isHorizontalLine)
                    {
                        // Intersection of both scanlines
                        scanlinesBuffer[y * _width + x] = intersectionColor;
                    }
                    else if (isVerticalLine)
                    {
                        scanlinesBuffer[y * _width + x] = verticalLineColor;
                    }
                    else if (isHorizontalLine)
                    {
                        scanlinesBuffer[y * _width + x] = horizontalLineColor;
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
    }
}
