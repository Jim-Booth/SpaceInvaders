// ============================================================================
// Project:     SpaceInvaders
// File:        Flags.cs
// Description: CPU status flags (Zero, Sign, Parity, Carry, Auxiliary Carry)
//              with update methods
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// Notice:      Space Invaders is (c) 1978 Taito Corporation.
//              This emulator is for educational purposes only.
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

        // 256-entry parity lookup: 1 = even parity (Intel 8080 convention), 0 = odd parity.
        // Generated once at class-load time; avoids a per-call bit-counting loop on the hot path.
        private static readonly uint[] ParityTable = BuildParityTable();

        private static uint[] BuildParityTable()
        {
            var table = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                int bits = 0;
                int n = i;
                while (n > 0) { bits++; n &= n - 1; } // Kernighan's popcount — runs once per entry
                table[i] = (uint)((bits % 2 == 0) ? 1 : 0);
            }
            return table;
        }

        public void UpdateZSP(uint value)
        {
            byte b = (byte)(value & 0xFF);
            _z = (uint)(b == 0 ? 1 : 0);
            _s = (uint)((b & 0x80) >> 7);
            _p = ParityTable[b];
        }

        public void UpdateAuxCarryFlag(byte a, byte b)
        {
            _ac = (uint)((((a & 0x0f) + (b & 0x0f)) > 0x0f) ? 1 : 0);
        }

        public void UpdateAuxCarryFlag(byte a, byte b, byte c)
        {
            _ac = (uint)((((a & 0x0f) + (b & 0x0f) + (c & 0x0f)) > 0x0f) ? 1 : 0);
        }

        // Kept for external callers (e.g. DAA in Intel8080.cs); also uses the table.
        public static uint CalculateParityFlag(byte value) => ParityTable[value];
    }
}