// ============================================================================
// Project:     SpaceInvaders
// File:        Registers.cs
// Description: CPU register definitions (A, B, C, D, E, H, L, PC, SP) and
//              register pair accessors
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// Notice:      Space Invaders is (c) 1978 Taito Corporation.
//              This emulator is for educational purposes only.
// ============================================================================

namespace SpaceInvaders.MAINBOARD
{
    public class Registers
    {
        private byte _a;  // Accumulator
        private byte _b;  // Register B (high byte of BC pair)
        private byte _c;  // Register C (low byte of BC pair)
        private byte _d;  // Register D (high byte of DE pair)
        private byte _e;  // Register E (low byte of DE pair)
        private byte _h;  // Register H (high byte of HL pair)
        private byte _l;  // Register L (low byte of HL pair)
        private uint _sp; // Stack pointer
        private uint _pc; // Program counter
        private bool _intEnable; // Interrupt enable flag

        public byte A
        {
            get => _a;
            set => _a = value;
        }

        public byte B
        {
            get => _b;
            set => _b = value;
        }

        public byte C
        {
            get => _c;
            set => _c = value;
        }

        public byte D
        {
            get => _d;
            set => _d = value;
        }

        public byte E
        {
            get => _e;
            set => _e = value;
        }

        public byte H
        {
            get => _h;
            set => _h = value;
        }

        public byte L
        {
            get => _l;
            set => _l = value;
        }

        public uint SP
        {
            get => _sp;
            set => _sp = value;
        }

        public uint PC
        {
            get => _pc;
            set => _pc = value;
        }

        public bool IntEnable
        {
            get => _intEnable;
            set => _intEnable = value;
        }

        public uint HL
        {
            get => (uint)_h << 8 | (uint)_l;
            set
            {
                _h = (byte)((value & 0xFF00) >> 8);
                _l = (byte)(value & 0x00FF);
            }
        }

        public uint DE
        {
            get => (uint)_d << 8 | (uint)_e;
            set
            {
                _d = (byte)((value & 0xFF00) >> 8);
                _e = (byte)(value & 0x00FF);
            }
        }

        public uint BC
        {
            get => (uint)_b << 8 | (uint)_c;
            set
            {
                _b = (byte)((value & 0xFF00) >> 8);
                _c = (byte)(value & 0x00FF);
            }
        }
    }
}