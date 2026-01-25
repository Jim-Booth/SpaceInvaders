// ============================================================================
// Project:     SpaceInvaders
// File:        Memory.cs
// Description: 64KB addressable memory implementation for the Intel 8080
//              emulator
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

namespace SpaceInvaders.MAINBOARD
{
    internal class Memory(long size)
    {
        private readonly byte[] _data = new byte[size];

        public byte[] Data => _data;

        public void LoadFromFile(string filePath, int addr, int length)
        {
            Array.Copy(File.ReadAllBytes(filePath), 0, _data, addr, length);
        }

        public byte ReadByte(uint addr)
        {
            return _data[addr];
        }

        public void WriteByte(uint addr, byte value)
        {
            _data[addr] = value;
        }
        
        /// <summary>
        /// Reads the high score from memory (0x20F4-0x20F5) as BCD and converts to integer.
        /// Space Invaders stores scores in BCD format with an implicit trailing zero.
        /// </summary>
        public int ReadHighScore()
        {
            // High score stored at 0x20F4 (low byte) and 0x20F5 (high byte) in BCD
            byte low = _data[0x20F4];
            byte high = _data[0x20F5];
            
            // Convert BCD to integer (each nibble is a digit)
            int score = ((high >> 4) & 0x0F) * 1000 +
                        (high & 0x0F) * 100 +
                        ((low >> 4) & 0x0F) * 10 +
                        (low & 0x0F);
            
            // Multiply by 10 because displayed scores have implicit trailing 0
            return score * 10;
        }
        
        /// <summary>
        /// Writes a high score value to memory (0x20F4-0x20F5) in BCD format.
        /// </summary>
        public void WriteHighScore(int score)
        {
            // Remove trailing zero (scores are always multiples of 10)
            int bcdValue = score / 10;
            
            // Convert integer to BCD (4 digits)
            byte low = (byte)(((bcdValue / 10) % 10 << 4) | (bcdValue % 10));
            byte high = (byte)(((bcdValue / 1000) % 10 << 4) | ((bcdValue / 100) % 10));
            
            _data[0x20F4] = low;
            _data[0x20F5] = high;
        }
    }
}
