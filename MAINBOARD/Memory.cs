
﻿namespace Invaders.MAINBOARD
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
