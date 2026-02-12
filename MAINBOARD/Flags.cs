// ============================================================================
// Project:     SpaceInvaders
// File:        Flags.cs
// Description: CPU status flags (Zero, Sign, Parity, Carry, Auxiliary Carry)
//              with update methods
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

namespace SpaceInvaders.MAINBOARD
{
    public class Flags
    {
        private uint _z;  // Zero bit
        private uint _s;  // Sign bit
        private uint _p;  // Parity bit
        private uint _cy; // Carry bit
        private uint _ac; // Auxiliary carry bit

        public uint Z
        {
            get => _z;
            set => _z = value;
        }

        public uint S
        {
            get => _s;
            set => _s = value;
        }

        public uint P
        {
            get => _p;
            set => _p = value;
        }

        public uint CY
        {
            get => _cy;
            set => _cy = value;
        }

        public uint AC
        {
            get => _ac;
            set => _ac = value;
        }

        public void UpdateCarryByte(uint value)
        {
            _cy = (uint)((value > 0x00FF) ? 1 : 0);
        }

        public void UpdateCarryWord(uint value)
        {
            _cy = (uint)((value > 0xFFFF) ? 1 : 0);
        }

        public void UpdateZSP(uint value)
        {
            _z = (uint)(((value & 0xFF) == 0) ? 1 : 0);
            _s = (uint)(((value & 0x80) == 0x80) ? 1 : 0);
            _p = CalculateParityFlag((byte)value);
        }

        public void UpdateAuxCarryFlag(byte a, byte b)
        {
            _ac = (uint)((((a & 0x0f) + (b & 0x0f)) > 0x0f) ? 1 : 0);
        }

        public void UpdateAuxCarryFlag(byte a, byte b, byte c)
        {
            _ac = (uint)((((a & 0x0f) + (b & 0x0f) + (c & 0x0f)) > 0x0f) ? 1 : 0);
        }

        public static uint CalculateParityFlag(byte value) // 1 is even
        {
            byte num = (byte)(value & 0xff);
            byte total;
            for (total = 0; num > 0; total++)
                num &= (byte)(num - 1);
            return (uint)(((total % 2) == 0) ? 1 : 0);
        }
    }
}