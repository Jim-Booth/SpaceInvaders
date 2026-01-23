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
        private readonly byte[] memory = new byte[size];

        public byte[] GetMemory
        {
            get { return memory; }
        }

        public void LoadFromFile(string filePath, int addr, int length)
        {
            Array.Copy(File.ReadAllBytes(filePath), 0, memory, addr, length);
        }

        public byte ReadByte(uint addr)
        {
            return memory[addr];
        }

        public void WriteByte(uint addr, byte value)
        {
            memory[addr] = value;
        }
    }
}
