// ============================================================================
// Project:     SpaceInvaders
// File:        Memory.cs
// Description: 64KB addressable memory implementation for the Intel 8080
//              emulator
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// Notice:      Space Invaders is (c) 1978 Taito Corporation.
//              This emulator is for educational purposes only.
// ============================================================================

namespace SpaceInvaders.MAINBOARD
{
    public class Memory(long size)
    {
        private readonly byte[] _data = new byte[size];

        public byte[] Data => _data;

        // Copies the specified number of bytes from the source array into memory starting at the given address.
        public void LoadFromBytes(byte[] data, int startAddress, int size)
        {
            for (int i = 0; i < size && i < data.Length; i++)
            {
                _data[startAddress + i] = data[i];
            }
        }

        // Reads a single byte from the specified memory address.
        public byte ReadByte(uint addr)
        {
            return _data[addr];
        }

        // Writes a single byte to the specified memory address.
        public void WriteByte(uint addr, byte value)
        {
            _data[addr] = value;
        }
        
        // Reads the high score from memory (0x20F4-0x20F5) as BCD and converts to integer.
        public int ReadHighScore()
        {
            byte low = _data[0x20F4];
            byte high = _data[0x20F5];
            
            int score = ((high >> 4) & 0x0F) * 1000 +
                        (high & 0x0F) * 100 +
                        ((low >> 4) & 0x0F) * 10 +
                        (low & 0x0F);
            
            return score * 10;
        }
        
        // Writes a high score value to memory (0x20F4-0x20F5) in BCD format.
        public void WriteHighScore(int score)
        {
            int bcdValue = score / 10;
            
            byte low = (byte)(((bcdValue / 10) % 10 << 4) | (bcdValue % 10));
            byte high = (byte)(((bcdValue / 1000) % 10 << 4) | ((bcdValue / 100) % 10));
            
            _data[0x20F4] = low;
            _data[0x20F5] = high;
        }
    }
}
