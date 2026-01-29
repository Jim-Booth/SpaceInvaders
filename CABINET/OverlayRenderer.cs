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
        private bool _fpsWarningEnabled = true;
        private int _frameCount = 0;
        private DateTime _lastFpsUpdate = DateTime.Now;
        private double _currentFps = 0.0;
        
        // Overlay message state
        private string? _message = null;
        private DateTime _messageEndTime;
        
        // DIP switch overlay state
        private bool _dipSwitchOverlayEnabled = false;
        
        // Controls help overlay state
        private bool _controlsOverlayEnabled = false;
        
        // Custom title bar settings
        private const int TitleBarBaseHeight = 20; // Fixed height in pixels (not scaled)
        private SDL.SDL_Rect _closeButtonRect;

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
        /// Gets or sets whether the low FPS warning is enabled (default: true).
        /// </summary>
        public bool FpsWarningEnabled
        {
            get => _fpsWarningEnabled;
            set => _fpsWarningEnabled = value;
        }
        
        /// <summary>
        /// Gets or sets whether the DIP switch overlay is displayed.
        /// </summary>
        public bool DipSwitchOverlayEnabled
        {
            get => _dipSwitchOverlayEnabled;
            set => _dipSwitchOverlayEnabled = value;
        }
        
        /// <summary>
        /// Gets or sets whether the controls help overlay is displayed.
        /// </summary>
        public bool ControlsOverlayEnabled
        {
            get => _controlsOverlayEnabled;
            set => _controlsOverlayEnabled = value;
        }
        
        /// <summary>
        /// Gets the title bar height in pixels (fixed size, not scaled).
        /// </summary>
        public static int GetTitleBarHeight(int screenMultiplier) => TitleBarBaseHeight;

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
            if (elapsed >= 2) // Update FPS every 0.5 seconds
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
            
            // Draw each character in green (standard overlay color)
            SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0x00, 0xFF);
            int charX = startX;
            foreach (char ch in _message)
            {
                if (ch == ' ')
                {
                    charX += 3 * screenMultiplier; // Narrower space between words
                }
                else
                {
                    DrawChar(ch, charX, startY, screenMultiplier);
                    charX += charWidth + charSpacing;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Draws the FPS counter in the top-right corner if enabled.
        /// </summary>
        public void DrawFpsCounter(int screenWidth, int screenMultiplier, int titleBarHeight)
        {
            if (!_fpsDisplayEnabled) return;
            
            // Format FPS string
            string fpsText = $"fps:{_currentFps:F1}";
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int totalWidth = fpsText.Length * (charWidth + charSpacing) - charSpacing;
            
            // Position in top-right corner with padding (below title bar)
            int padding = 5 * screenMultiplier;
            int startX = screenWidth - totalWidth - padding;
            int startY = titleBarHeight + padding;
            
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
            int charX = startX;
            foreach (char ch in fpsText)
            {
                if (ch == ' ')
                {
                    charX += 3 * screenMultiplier; // Narrower space between words
                }
                else
                {
                    DrawChar(ch, charX, startY, screenMultiplier);
                    charX += charWidth + charSpacing;
                }
            }
        }

        /// <summary>
        /// Draws a low FPS warning message at the top of the screen.
        /// Displays continuously while FPS is low.
        /// </summary>
        public void DrawLowFpsWarning(int screenWidth, int screenMultiplier, int titleBarHeight)
        {
            string warningText = "low fps! press r to disable crt";
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int totalWidth = warningText.Length * (charWidth + charSpacing) - charSpacing;
            
            // Position at top center (below title bar)
            int startX = (screenWidth - totalWidth) / 2;
            int startY = titleBarHeight + 10 * screenMultiplier;
            
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
            
            // Draw warning text in red
            SDL.SDL_SetRenderDrawColor(_renderer, 0xFF, 0x40, 0x40, 0xFF);
            int charX = startX;
            foreach (char ch in warningText)
            {
                if (ch == ' ')
                {
                    charX += 3 * screenMultiplier; // Narrower space between words
                }
                else
                {
                    DrawChar(ch, charX, startY, screenMultiplier);
                    charX += charWidth + charSpacing;
                }
            }
        }
        
        /// <summary>
        /// Draws the settings overlay showing DIP switches and display controls.
        /// </summary>
        public void DrawDipSwitchOverlay(int screenWidth, int screenHeight, int screenMultiplier, 
            GameSettings settings, bool crtEnabled, bool soundEnabled, bool backgroundEnabled)
        {
            if (!_dipSwitchOverlayEnabled) return;
            
            // Build the display lines
            string[] lines = [
                "dip switches",
                $"  lives: {settings.ActualLives}",
                $"  bonus: {settings.BonusLifeThreshold}",
                $"  coininfo: {(settings.CoinInfoHidden ? "off" : "on")}",
                "",
                "display",
                $"  scale: {screenMultiplier}x",
                $"  crt: {(crtEnabled ? "on" : "off")}",
                $"  sound: {(soundEnabled ? "on" : "off")}",
                $"  background: {(backgroundEnabled ? "on" : "off")}"
            ];
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int lineSpacing = 2 * screenMultiplier;
            
            // Find the widest line
            int maxWidth = 0;
            foreach (var line in lines)
            {
                int lineWidth = line.Length * (charWidth + charSpacing) - charSpacing;
                if (lineWidth > maxWidth) maxWidth = lineWidth;
            }
            
            // Calculate total height
            int totalHeight = lines.Length * charHeight + (lines.Length - 1) * lineSpacing;
            
            // Center the overlay on screen
            int startX = (screenWidth - maxWidth) / 2;
            int startY = (screenHeight - totalHeight) / 2;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 180);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 6 * screenMultiplier,
                y = startY - 4 * screenMultiplier,
                w = maxWidth + 12 * screenMultiplier,
                h = totalHeight + 8 * screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw each line
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrEmpty(line)) continue; // Skip empty separator line
                
                int lineY = startY + lineIndex * (charHeight + lineSpacing);
                
                // Title lines in cyan, values in green
                bool isTitle = line == "dip switches" || line == "display";
                if (isTitle)
                    SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0xFF, 0xFF); // Cyan for titles
                else
                    SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0x00, 0xFF); // Green for values
                
                int charX = startX;
                foreach (char ch in line)
                {
                    if (ch == ' ')
                    {
                        charX += 3 * screenMultiplier; // Narrower space between words
                    }
                    else
                    {
                        DrawChar(ch, charX, lineY, screenMultiplier);
                        charX += charWidth + charSpacing;
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws the custom title bar with title text and close button.
        /// Returns the close button rectangle for hit testing.
        /// </summary>
        public SDL.SDL_Rect DrawTitleBar(int screenWidth, int screenMultiplier, string title)
        {
            int titleBarHeight = TitleBarBaseHeight;
            
            // Draw title bar background (dark gray gradient effect)
            SDL.SDL_SetRenderDrawColor(_renderer, 50, 50, 55, 255);
            SDL.SDL_Rect titleBarRect = new SDL.SDL_Rect { x = 0, y = 0, w = screenWidth, h = titleBarHeight };
            SDL.SDL_RenderFillRect(_renderer, ref titleBarRect);
            
            // Draw subtle top highlight
            SDL.SDL_SetRenderDrawColor(_renderer, 70, 70, 75, 255);
            SDL.SDL_RenderDrawLine(_renderer, 0, 0, screenWidth, 0);
            
            // Draw title text using full-height font (centered vertically)
            int fontHeight = 10;
            int fontWidth = 5;
            int textY = (titleBarHeight - fontHeight) / 2;
            int textX = 8;
            
            // Draw Space Invaders icon first
            SDL.SDL_SetRenderDrawColor(_renderer, 0x0F, 0xDF, 0x0F, 255); // Green like the game
            DrawInvaderIcon(textX, textY);
            textX += 14; // Icon width (11) + spacing (3)
            
            // Draw title text
            SDL.SDL_SetRenderDrawColor(_renderer, 180, 180, 185, 255);
            foreach (char c in title)
            {
                if (c == ' ')
                {
                    textX += 4; // Narrower space between words
                }
                else
                {
                    DrawTitleChar(c, textX, textY);
                    textX += fontWidth + 2; // 5 pixels wide + 2 pixel spacing
                }
            }
            
            // Draw close button on the right
            int buttonSize = 14;
            int buttonX = screenWidth - buttonSize - 4;
            int buttonY = (titleBarHeight - buttonSize) / 2;
            
            // Close button background (dark red)
            SDL.SDL_SetRenderDrawColor(_renderer, 160, 50, 50, 255);
            _closeButtonRect = new SDL.SDL_Rect { x = buttonX, y = buttonY, w = buttonSize, h = buttonSize };
            SDL.SDL_RenderFillRect(_renderer, ref _closeButtonRect);
            
            // Close button border
            SDL.SDL_SetRenderDrawColor(_renderer, 200, 70, 70, 255);
            SDL.SDL_RenderDrawRect(_renderer, ref _closeButtonRect);
            
            // Draw X using custom 10x10 font, centered in button
            SDL.SDL_SetRenderDrawColor(_renderer, 255, 255, 255, 255);
            int xCharX = buttonX + (buttonSize - 10) / 2;
            int xCharY = buttonY + (buttonSize - 10) / 2;
            DrawCloseButtonX(xCharX, xCharY);
            
            // Draw subtle bottom border
            SDL.SDL_SetRenderDrawColor(_renderer, 30, 30, 35, 255);
            SDL.SDL_RenderDrawLine(_renderer, 0, titleBarHeight - 1, screenWidth, titleBarHeight - 1);
            
            return _closeButtonRect;
        }
        
        /// <summary>
        /// Draws a single character using a 5x10 pixel font designed for the title bar.
        /// Full-height characters for maximum readability at native resolution.
        /// </summary>
        private void DrawTitleChar(char c, int x, int y, int scale = 1)
        {
            // 5x10 pixel font - each row is 5 bits (0x10 = leftmost, 0x01 = rightmost)
            // Designed for clean rendering at 1:1 pixel ratio
            byte[] pattern = char.ToUpper(c) switch
            {
                'A' => [0x0E, 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11, 0x11, 0x00],
                'B' => [0x1E, 0x11, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x11, 0x1E, 0x00],
                'C' => [0x0E, 0x11, 0x10, 0x10, 0x10, 0x10, 0x10, 0x11, 0x0E, 0x00],
                'D' => [0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E, 0x00],
                'E' => [0x1F, 0x10, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10, 0x1F, 0x00],
                'F' => [0x1F, 0x10, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10, 0x10, 0x00],
                'G' => [0x0E, 0x11, 0x10, 0x10, 0x17, 0x11, 0x11, 0x11, 0x0F, 0x00],
                'H' => [0x11, 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11, 0x11, 0x00],
                'I' => [0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E, 0x00],
                'J' => [0x07, 0x02, 0x02, 0x02, 0x02, 0x02, 0x12, 0x12, 0x0C, 0x00],
                'K' => [0x11, 0x12, 0x14, 0x18, 0x18, 0x14, 0x12, 0x11, 0x11, 0x00],
                'L' => [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F, 0x00],
                'M' => [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11, 0x11, 0x11, 0x00],
                'N' => [0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11, 0x11, 0x00],
                'O' => [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E, 0x00],
                'P' => [0x1E, 0x11, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10, 0x10, 0x00],
                'Q' => [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D, 0x00],
                'R' => [0x1E, 0x11, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11, 0x11, 0x00],
                'S' => [0x0E, 0x11, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x11, 0x0E, 0x00],
                'T' => [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x00],
                'U' => [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E, 0x00],
                'V' => [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x0A, 0x04, 0x00],
                'W' => [0x11, 0x11, 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11, 0x00],
                'X' => [0x11, 0x11, 0x0A, 0x0A, 0x04, 0x0A, 0x0A, 0x11, 0x11, 0x00],
                'Y' => [0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04, 0x04, 0x00],
                'Z' => [0x1F, 0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10, 0x1F, 0x00],
                '0' => [0x0E, 0x11, 0x11, 0x13, 0x15, 0x19, 0x11, 0x11, 0x0E, 0x00],
                '1' => [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E, 0x00],
                '2' => [0x0E, 0x11, 0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F, 0x00],
                '3' => [0x0E, 0x11, 0x01, 0x01, 0x06, 0x01, 0x01, 0x11, 0x0E, 0x00],
                '4' => [0x02, 0x06, 0x0A, 0x12, 0x12, 0x1F, 0x02, 0x02, 0x02, 0x00],
                '5' => [0x1F, 0x10, 0x10, 0x1E, 0x01, 0x01, 0x01, 0x11, 0x0E, 0x00],
                '6' => [0x06, 0x08, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x11, 0x0E, 0x00],
                '7' => [0x1F, 0x01, 0x01, 0x02, 0x04, 0x04, 0x08, 0x08, 0x08, 0x00],
                '8' => [0x0E, 0x11, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x11, 0x0E, 0x00],
                '9' => [0x0E, 0x11, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x02, 0x0C, 0x00],
                '-' => [0x00, 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00, 0x00, 0x00],
                ' ' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
                _ => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
            };
            
            for (int row = 0; row < 10; row++)
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
        
        /// <summary>
        /// Draws a 10x10 pixel 'X' character for the close button.
        /// Designed to be visually clean and centered in the button.
        /// </summary>
        private void DrawCloseButtonX(int x, int y)
        {
            // 10x10 pixel X pattern - clean diagonal lines with 2-pixel thickness
            // Each row uses 10 bits represented as a ushort (0x200 = leftmost, 0x001 = rightmost)
            ushort[] pattern = [
                0b1100000011,  // Row 0: ##......##
                0b0110000110,  // Row 1: .##....##.
                0b0011001100,  // Row 2: ..##..##..
                0b0001111000,  // Row 3: ...####...
                0b0000110000,  // Row 4: ....##....
                0b0000110000,  // Row 5: ....##....
                0b0001111000,  // Row 6: ...####...
                0b0011001100,  // Row 7: ..##..##..
                0b0110000110,  // Row 8: .##....##.
                0b1100000011,  // Row 9: ##......##
            ];
            
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    if ((pattern[row] & (0x200 >> col)) != 0)
                    {
                        SDL.SDL_Rect pixelRect = new SDL.SDL_Rect
                        {
                            x = x + col,
                            y = y + row,
                            w = 1,
                            h = 1
                        };
                        SDL.SDL_RenderFillRect(_renderer, ref pixelRect);
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws the iconic Space Invaders alien sprite (11x10 pixels).
        /// Based on the classic "crab" invader design.
        /// </summary>
        private void DrawInvaderIcon(int x, int y)
        {
            // 11x10 pixel Space Invader (crab type) - the most iconic design
            // Each row uses 11 bits represented as a ushort (0x400 = leftmost, 0x001 = rightmost)
            ushort[] pattern = [
                0b00100000100,  // Row 0:   #.....#
                0b00010001000,  // Row 1:    #...#
                0b00111111100,  // Row 2:   #######
                0b01101110110,  // Row 3:  ## ### ##
                0b11111111111,  // Row 4: ###########
                0b10111111101,  // Row 5: # ####### #
                0b10100000101,  // Row 6: # #.....# #
                0b10100000101,  // Row 7: # #.....# #
                0b00011011000,  // Row 8:    ## ##
                0b00000000000,  // Row 9: (empty row for spacing)
            ];
            
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 11; col++)
                {
                    if ((pattern[row] & (0x400 >> col)) != 0)
                    {
                        SDL.SDL_Rect pixelRect = new SDL.SDL_Rect
                        {
                            x = x + col,
                            y = y + row,
                            w = 1,
                            h = 1
                        };
                        SDL.SDL_RenderFillRect(_renderer, ref pixelRect);
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks if a point is inside the close button.
        /// </summary>
        public bool IsPointInCloseButton(int x, int y)
        {
            return x >= _closeButtonRect.x && x < _closeButtonRect.x + _closeButtonRect.w &&
                   y >= _closeButtonRect.y && y < _closeButtonRect.y + _closeButtonRect.h;
        }
        
        /// <summary>
        /// Draws the controls help overlay showing all keyboard controls.
        /// </summary>
        public void DrawControlsOverlay(int screenWidth, int screenHeight, int screenMultiplier)
        {
            if (!_controlsOverlayEnabled) return;
            
            // Build the display lines
            string[] lines = [
                "game controls",
                "  c-coin  1-1p  2-2p",
                "  p1 arrows-move  space-fire",
                "  p2 a/d-move  w-fire",
                "  p-pause  t-tilt  esc-quit",
                "",
                "display controls",
                "  r-crt ",
                "  s-sound",
                "  b-background",
                "  f-fps",
                "  []-scale",
                "",
                "function keys",
                "  f1-lives ","  f2-bonus","  f3-coininfo",
                "  f4-fps warning","  f5-settings"
            ];
            
            // Character dimensions (scaled)
            int charWidth = 5 * screenMultiplier;
            int charHeight = 7 * screenMultiplier;
            int charSpacing = 1 * screenMultiplier;
            int lineSpacing = 2 * screenMultiplier;
            
            // Find the widest line
            int maxWidth = 0;
            foreach (var line in lines)
            {
                int lineWidth = line.Length * (charWidth + charSpacing) - charSpacing;
                if (lineWidth > maxWidth) maxWidth = lineWidth;
            }
            
            // Calculate total height
            int totalHeight = lines.Length * charHeight + (lines.Length - 1) * lineSpacing;
            
            // Center the overlay on screen
            int startX = (screenWidth - maxWidth) / 2;
            int startY = (screenHeight - totalHeight) / 2;
            
            // Draw semi-transparent background box
            SDL.SDL_SetRenderDrawBlendMode(_renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 200);
            SDL.SDL_Rect bgRect = new SDL.SDL_Rect
            {
                x = startX - 10 * screenMultiplier,
                y = startY - 8 * screenMultiplier,
                w = maxWidth + 20 * screenMultiplier,
                h = totalHeight + 16 * screenMultiplier
            };
            SDL.SDL_RenderFillRect(_renderer, ref bgRect);
            
            // Draw each line
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrEmpty(line)) continue; // Skip empty separator line
                
                int lineY = startY + lineIndex * (charHeight + lineSpacing);
                
                // Title lines in cyan, values in green
                bool isTitle = line == "game controls" || line == "display controls" || line == "function keys";
                if (isTitle)
                    SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0xFF, 0xFF); // Cyan for titles
                else
                    SDL.SDL_SetRenderDrawColor(_renderer, 0x00, 0xFF, 0x00, 0xFF); // Green for controls
                
                int charX = startX;
                foreach (char ch in line)
                {
                    if (ch == ' ')
                    {
                        charX += 3 * screenMultiplier; // Narrower space between words
                    }
                    else
                    {
                        DrawChar(ch, charX, lineY, screenMultiplier);
                        charX += charWidth + charSpacing;
                    }
                }
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
                '[' => [0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E],
                ']' => [0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E],
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
