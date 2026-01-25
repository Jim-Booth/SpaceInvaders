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

namespace SpaceInvaders
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
        private readonly float BlurStrength = 0.3f;
        private readonly float BloomAlpha = 120;
        
        private readonly Random _random = new();
        private readonly DateTime _startupTime;
        
        // SDL textures for overlay effects
        private IntPtr _vignetteTexture;
        private IntPtr _screenMaskTexture;
        private IntPtr _renderer;
        
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
            
            CreateVignetteTexture();
            CreateScreenMaskTexture();
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
            
            CreateVignetteTexture();
            CreateScreenMaskTexture();
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
        /// Applies post-processing effects: bloom, blur, flicker, jitter, and warmup.
        /// </summary>
        public void ApplyPostProcessing(uint[] pixelBuffer, int width, int height)
        {
            if (!Enabled) return;
            
            ApplyBloomEffect(pixelBuffer, width, height);
            
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
                    
                    if (BlurStrength > 0 && x > 0 && x < width - 1)
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
                            
                            float centerWeight = 1.0f - BlurStrength;
                            float sideWeight = BlurStrength * 0.5f;
                            
                            r = (byte)(r * centerWeight + (lr + rr) * sideWeight);
                            g = (byte)(g * centerWeight + (lg + rg) * sideWeight);
                            b = (byte)(b * centerWeight + (lb + rb) * sideWeight);
                        }
                    }
                    
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

        /// <summary>
        /// Renders scanlines and overlay textures (vignette, screen mask).
        /// Call after rendering the game texture.
        /// </summary>
        public void RenderOverlays(IntPtr renderer, int width, int height, int screenMultiplier)
        {
            if (!Enabled) return;
            
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            
            // Vertical scanlines
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 35);
            for (int x = 0; x < width; x += screenMultiplier)
            {
                SDL.SDL_RenderDrawLine(renderer, x, 0, x, height);
            }
            
            // Horizontal scanlines
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 25);
            for (int y = 0; y < height; y += screenMultiplier)
            {
                SDL.SDL_RenderDrawLine(renderer, 0, y, width, y);
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

        private void ApplyBloomEffect(uint[] pixelBuffer, int width, int height)
        {
            uint[] bloomBuffer = new uint[pixelBuffer.Length];
            Array.Copy(pixelBuffer, bloomBuffer, pixelBuffer.Length);
            
            int step = _screenMultiplier;
            
            for (int y = step; y < height - step; y += step)
            {
                for (int x = step; x < width - step; x += step)
                {
                    int index = y * width + x;
                    uint pixel = pixelBuffer[index];
                    
                    if (pixel == 0) continue;
                    
                    byte r = (byte)((pixel >> 16) & 0xFF);
                    byte g = (byte)((pixel >> 8) & 0xFF);
                    byte b = (byte)(pixel & 0xFF);
                    
                    float brightness = Math.Max(r, Math.Max(g, b)) / 255.0f;
                    if (brightness < 0.5f) continue;
                    
                    byte glowR = r;
                    byte glowG = g;
                    byte glowB = b;
                    byte glowA = (byte)BloomAlpha;
                    
                    int[] offsets = { -width, width, -1, 1, -width-1, -width+1, width-1, width+1 };
                    foreach (int offset in offsets)
                    {
                        int neighborIndex = index + offset;
                        if (neighborIndex < 0 || neighborIndex >= pixelBuffer.Length) continue;
                        
                        uint neighbor = bloomBuffer[neighborIndex];
                        
                        if (neighbor == 0)
                        {
                            bloomBuffer[neighborIndex] = ((uint)glowA << 24) | ((uint)glowR << 16) | ((uint)glowG << 8) | glowB;
                        }
                    }
                }
            }
            
            Array.Copy(bloomBuffer, pixelBuffer, pixelBuffer.Length);
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
        }
    }
}
