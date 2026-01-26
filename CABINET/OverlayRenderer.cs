// ============================================================================
// Project:     SpaceInvaders
// File:        OverlayRenderer.cs
// Description: Handles rendering of overlay text messages and FPS counter
//              using a simple 5x7 pixel font
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using SDL2;

namespace SpaceInvaders.CABINET
{
    /// <summary>
    /// Renders overlay text messages and FPS counter on the game display.
    /// </summary>
    public class OverlayRenderer
    {
        private readonly IntPtr _renderer;
        
        // FPS counter state
        private bool _fpsDisplayEnabled = false;
        private int _frameCount = 0;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private double _currentFps = 0.0;
        
        // Overlay message state
        private string? _message = null;
        private DateTime _messageEndTime;

        public OverlayRenderer(IntPtr renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// Gets or sets whether the FPS counter is displayed.
        /// </summary>
        public bool FpsDisplayEnabled
        {
            get => _fpsDisplayEnabled;
            set => _fpsDisplayEnabled = value;
        }

        /// <summary>
        /// Gets the current measured FPS.
        /// </summary>
        public double CurrentFps => _currentFps;

        /// <summary>
        /// Gets the current overlay message, or null if none.
        /// </summary>
        public string? Message => _message;

        /// <summary>
        /// Shows a temporary overlay message for the specified duration.
        /// </summary>
        public void ShowMessage(string message, TimeSpan duration)
        {
            _message = message;
            _messageEndTime = DateTime.Now + duration;
        }

        /// <summary>
        /// Shows a persistent overlay message (e.g., for pause state).
        /// </summary>
        public void ShowPersistentMessage(string message)
        {
            _message = message;
            _messageEndTime = DateTime.MaxValue;
        }

        /// <summary>
        /// Clears any active overlay message.
        /// </summary>
        public void ClearMessage()
        {
            _message = null;
        }

        /// <summary>
        /// Updates and renders the FPS counter. Call once per frame.
        /// </summary>
        public void UpdateFps()
        {
            _frameCount++;
            var now = DateTime.Now;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;
            if (elapsed >= 0.5) // Update FPS every 0.5 seconds
            {
                _currentFps = _frameCount / elapsed;
                _frameCount = 0;
                _lastFpsUpdate = now;
            }
        }

        /// <summary>
        /// Draws the overlay message if active and not expired.
        /// </summary>
        /// <returns>True if the message is still active, false if it expired.</returns>
        public bool DrawMessage(int screenWidth, int screenHeight, int screenMultiplier)
        {
            if (_message == null) return false;
            
            if (DateTime.Now >= _messageEndTime)
            {
                _message = null;
                return false;
            }
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int totalWidth = _message.Length * (charWidth + charSpacing) - charSpacing;
            
            // Center position
            int startX = (screenWidth - totalWidth) / 2;
            int startY = (screenHeight - charHeight) / 2;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 180);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 10 * screenMultiplier,
                y = startY - 5 * screenMultiplier,
                w = totalWidth + 20 * screenMultiplier,
                h = charHeight + 10 * screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw each character in yellow
            SDL.SDL_SetRenderDrawColor(_renderer, 0xFF, 0xFF, 0x00, 0xFF);
            for (int i = 0; i < _message.Length; i++)
            {
                int charX = startX + i * (charWidth + charSpacing);
                DrawChar(_message[i], charX, startY, screenMultiplier);
            }
            
            return true;
        }

        /// <summary>
        /// Draws the FPS counter in the top-right corner if enabled.
        /// </summary>
        public void DrawFpsCounter(int screenWidth, int screenMultiplier)
        {
            if (!_fpsDisplayEnabled) return;
            
            // Format FPS string
            string fpsText = $"fps:{_currentFps:F1}";
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int totalWidth = fpsText.Length * (charWidth + charSpacing) - charSpacing;
            
            // Position in top-right corner with padding
            int padding = 5 * screenMultiplier;
            int startX = screenWidth - totalWidth - padding;
            int startY = padding;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 150);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 3 * screenMultiplier,
                y = startY - 2 * screenMultiplier,
                w = totalWidth + 6 * screenMultiplier,
                h = charHeight + 4 * screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw FPS text in green
            SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0x00, 0xFF);
            for (int i = 0; i < fpsText.Length; i++)
            {
                int charX = startX + i * (charWidth + charSpacing);
                DrawChar(fpsText[i], charX, startY, screenMultiplier);
            }
        }

        /// <summary>
        /// Draws a low FPS warning message at the top of the screen.
        /// Displays continuously while FPS is low.
        /// </summary>
        public void DrawLowFpsWarning(int screenWidth, int screenHeight, int screenMultiplier)
        {
            string warningText = "low fps! press r to disable crt";
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int totalWidth = warningText.Length * (charWidth + charSpacing) - charSpacing;
            
            // Position at top center
            int startX = (screenWidth - totalWidth) / 2;
            int startY = 20 * screenMultiplier;
            
            // Draw semi-transparent red background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 80, 0, 0, 200);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 8 * screenMultiplier,
                y = startY - 4 * screenMultiplier,
                w = totalWidth + 16 * screenMultiplier,
                h = charHeight + 8 * screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw warning text in bright red/orange
            SDL.SDL_SetRenderDrawColor(_renderer, 0xFF, 0x66, 0x00, 0xFF);
            for (int i = 0; i < warningText.Length; i++)
            {
                int charX = startX + i * (charWidth + charSpacing);
                DrawChar(warningText[i], charX, startY, screenMultiplier);
            }
        }

        /// <summary>
        /// Draws a single character using the 5x7 pixel font.
        /// </summary>
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
    }
}
