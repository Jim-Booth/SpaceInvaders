// ============================================================================
// Project:     SpaceInvaders
// File:        Settings.cs
// Description: Game settings including DIP switch configuration with JSON
//              persistence
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using System.Text.Json;

namespace SpaceInvaders
{
    /// <summary>
    /// DIP switch and game settings with JSON file persistence.
    /// Simulates the physical DIP switches found on the original arcade PCB.
    /// </summary>
    public class GameSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        
        // DIP Switch 1: Number of lives (0=3, 1=4, 2=5, 3=6)
        public int Lives { get; set; } = 0;  // Default: 3 lives
        
        // DIP Switch 2: Bonus life threshold (false=1500, true=1000)
        public bool BonusLifeAt1000 { get; set; } = false;  // Default: 1500
        
        // DIP Switch 3: Coin info display in demo (false=show, true=hide)
        public bool CoinInfoHidden { get; set; } = false;  // Default: show
        
        /// <summary>
        /// Gets the actual number of lives based on DIP switch setting.
        /// </summary>
        public int ActualLives => Lives + 3;
        
        /// <summary>
        /// Gets the bonus life threshold based on DIP switch setting.
        /// </summary>
        public int BonusLifeThreshold => BonusLifeAt1000 ? 1000 : 1500;
        
        /// <summary>
        /// Calculates the Port 2 DIP switch byte value.
        /// Bits 0-1: Lives, Bit 3: Bonus life, Bit 7: Coin info
        /// </summary>
        public byte GetPort2DipBits()
        {
            byte value = (byte)(Lives & 0x03);  // Bits 0-1: lives
            if (BonusLifeAt1000)
                value |= 0x08;  // Bit 3: bonus life at 1000
            if (CoinInfoHidden)
                value |= 0x80;  // Bit 7: hide coin info
            return value;
        }
        
        /// <summary>
        /// Cycles through lives options: 3 -> 4 -> 5 -> 6 -> 3
        /// </summary>
        public void CycleLives()
        {
            Lives = (Lives + 1) % 4;
        }
        
        /// <summary>
        /// Toggles bonus life threshold between 1000 and 1500.
        /// </summary>
        public void ToggleBonusLife()
        {
            BonusLifeAt1000 = !BonusLifeAt1000;
        }
        
        /// <summary>
        /// Toggles coin info display visibility.
        /// </summary>
        public void ToggleCoinInfo()
        {
            CoinInfoHidden = !CoinInfoHidden;
        }
        
        /// <summary>
        /// Loads settings from settings.json, or returns defaults if not found.
        /// </summary>
        public static GameSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<GameSettings>(json);
                    if (settings != null)
                    {
                        Console.WriteLine($"Loaded settings: Lives={settings.ActualLives}, Bonus={settings.BonusLifeThreshold}, CoinInfo={!settings.CoinInfoHidden}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
            
            Console.WriteLine("Using default settings");
            return new GameSettings();
        }
        
        /// <summary>
        /// Saves current settings to settings.json.
        /// </summary>
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
