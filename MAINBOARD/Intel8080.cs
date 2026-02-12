// ============================================================================
// Project:     SpaceInvaders
// File:        Intel8080.cs
// Description: Intel 8080 CPU emulator core with full opcode implementation,
//              interrupt handling, and cycle-accurate timing
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using System.Diagnostics;

namespace SpaceInvaders.MAINBOARD
{
    public class Intel8080
    {
        private bool _running;
        private bool _paused;

        public bool Running
        {
            get => _running;
            set => _running = value;
        }

        public bool Paused
        {
            get => _paused;
            set => _paused = value;
        }

        private byte[] _portIn = new byte[4]; // 0,1,2,3

        public byte[] PortIn
        {
            set => _portIn = value;
            get => _portIn;
        }

        private readonly byte[] _portOut = new byte[7]; // 2,3,5,6

        public byte[] PortOut => _portOut;

        private readonly Memory _memory;

        public Memory Memory => _memory;

        private readonly byte[] _video;

        public byte[] Video => _video;

        private readonly AutoResetEvent _displayTiming = new(false);

        public AutoResetEvent DisplayTiming => _displayTiming;

        private readonly AutoResetEvent _soundTiming = new(false);

        public AutoResetEvent SoundTiming => _soundTiming;

        private readonly Registers _registers = new();
        private readonly Flags _flags = new();
        private readonly uint _videoStartAddress;
        private int _hardwareShiftRegisterData;
        private int _hardwareShiftRegisterOffset;
        private static readonly int ClockSpeed = 2000000; // 2 Mhz
        private static readonly int Frequency = 60; //60 Hz
        private static readonly int HalfFrameCyclesMax = (ClockSpeed / Frequency) / 2;// 2,000,000/60 = 33,333/2 = 16,666
        private readonly int FrameTimeMs = 1000 / Frequency; // 1/60 = 16.7ms
        public int FrameTiming => FrameTimeMs;
        private readonly Stopwatch _frameTiming = new();

        public Intel8080(Memory memory)
        {
            _memory = memory;
            _video = new byte[0x1C00];
            _videoStartAddress = 0x2400;
            _registers.PC = 0x0000;
        }

        /// <summary>
        /// Executes a single frame worth of CPU cycles (for browser single-threaded use).
        /// Returns true if frame is ready to render.
        /// </summary>
        public bool RunFrame()
        {
            if (_paused) return false;
            
            ExecuteCycles(HalfFrameCyclesMax);  // 1st half of frame
            Interrupt(1);                        // mid screen Interrupt
            ExecuteCycles(HalfFrameCyclesMax);  // 2nd half of frame
            Interrupt(2);                        // full screen interrupt
            Buffer.BlockCopy(_memory.Data, (int)_videoStartAddress, _video, 0, _video.Length);
            return true;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _running = true;
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                // Wait while paused
                while (_paused && _running && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(16, cancellationToken);
                }
                if (!_running || cancellationToken.IsCancellationRequested) break;
                
                _frameTiming.Restart();// frame start
                ExecuteCycles(HalfFrameCyclesMax);// 1st half of frame
                Interrupt(1);// mid screen Interrupt
                ExecuteCycles(HalfFrameCyclesMax); // 2nd half of frame
                Interrupt(2);// full screen interrupt
                Buffer.BlockCopy(_memory.Data, (int)_videoStartAddress, _video, 0, _video.Length); // draw the video
                _displayTiming.Set();// signal the display to draw (non blocking)
                int mS = (FrameTimeMs - (int)_frameTiming.ElapsedMilliseconds);
                if (mS > 0)
                    await Task.Delay(mS, cancellationToken);
            }
        }

        private void ExecuteCycles(int maxCycles)
        {
            int cycles = 0;
            while (_running && cycles < maxCycles)
            {
                byte opcode = _memory.ReadByte(_registers.PC);
                cycles += CallOpcode(opcode);
                _registers.PC++;
            }
        }

        public void Stop()
        {
            _running = false;
        }

        private ushort ReadOpcodeDataWord()
        {
            return (ushort)(_memory.ReadByte(_registers.PC + 2) << 8 | _memory.ReadByte(_registers.PC + 1));
        }

        private void Call(ushort address, ushort retAddress)
        {
            _memory.WriteByte(_registers.SP - 1, (byte)((retAddress >> 8) & 0xFF));
            _memory.WriteByte(_registers.SP - 2, (byte)(retAddress & 0xFF));
            _registers.PC = address;
            _registers.SP -= 2;
        }

        private void Interrupt(int addr)
        {
            if (_registers.IntEnable)
            {
                _memory.WriteByte(_registers.SP - 1, (byte)((_registers.PC >> 8) & 0xFF));
                _memory.WriteByte(_registers.SP - 2, (byte)(_registers.PC & 0xFF));
                _registers.SP -= 2;
                if (addr == 1)
                    _registers.PC = 0x0008;
                if (addr == 2)
                    _registers.PC = 0x0010;
            }
        }

        private int CallOpcode(byte opcode)
        {
            return opcode switch
            {
                0x00 => OP_00(),
                0x01 => OP_01(),
                0x02 => OP_02(),
                0x03 => OP_03(),
                0x04 => OP_04(),
                0x05 => OP_05(),
                0x06 => OP_06(),
                0x07 => OP_07(),
                0x09 => OP_09(),
                0x0A => OP_0A(),
                0x0B => OP_0B(),
                0x0C => OP_0C(),
                0x0D => OP_0D(),
                0x0E => OP_0E(),
                0x0F => OP_0F(),
                0x11 => OP_11(),
                0x12 => OP_12(),
                0x13 => OP_13(),
                0x14 => OP_14(),
                0x15 => OP_15(),
                0x16 => OP_16(),
                0x17 => OP_17(),
                0x19 => OP_19(),
                0x1A => OP_1A(),
                0x1B => OP_1B(),
                0x1C => OP_1C(),
                0x1D => OP_1D(),
                0x1E => OP_1E(),
                0x1F => OP_1F(),
                0x20 => OP_20(),// RIM	1		special
                0x21 => OP_21(),
                0x22 => OP_22(),
                0x23 => OP_23(),
                0x24 => OP_24(),
                0x25 => OP_25(),
                0x26 => OP_26(),
                0x27 => OP_27(),
                0x29 => OP_29(),
                0x2A => OP_2A(),
                0x2B => OP_2B(),
                0x2C => OP_2C(),
                0x2D => OP_2D(),
                0x2E => OP_2E(),
                0x2F => OP_2F(),
                0x30 => OP_30(),// SIM	1		special
                0x31 => OP_31(),
                0x32 => OP_32(),
                0x33 => OP_33(),
                0x34 => OP_34(),
                0x35 => OP_35(),
                0x36 => OP_36(),
                0x37 => OP_37(),
                0x39 => OP_39(),
                0x3A => OP_3A(),
                0x3B => OP_3B(),
                0x3C => OP_3C(),
                0x3D => OP_3D(),
                0x3E => OP_3E(),
                0x3F => OP_3F(),
                0x40 => OP_40(),
                0x41 => OP_41(),
                0x42 => OP_42(),
                0x43 => OP_43(),
                0x44 => OP_44(),
                0x45 => OP_45(),
                0x46 => OP_46(),
                0x47 => OP_47(),
                0x48 => OP_48(),
                0x49 => OP_49(),
                0x4A => OP_4A(),
                0x4B => OP_4B(),
                0x4C => OP_4C(),
                0x4D => OP_4D(),
                0x4E => OP_4E(),
                0x4F => OP_4F(),
                0x50 => OP_50(),
                0x51 => OP_51(),
                0x52 => OP_52(),
                0x53 => OP_53(),
                0x54 => OP_54(),
                0x55 => OP_55(),
                0x56 => OP_56(),
                0x57 => OP_57(),
                0x58 => OP_58(),
                0x59 => OP_59(),
                0x5A => OP_5A(),
                0x5B => OP_5B(),
                0x5C => OP_5C(),
                0x5D => OP_5D(),
                0x5E => OP_5E(),
                0x5F => OP_5F(),
                0x60 => OP_60(),
                0x61 => OP_61(),
                0x62 => OP_62(),
                0x63 => OP_63(),
                0x64 => OP_64(),
                0x65 => OP_65(),
                0x66 => OP_66(),
                0x67 => OP_67(),
                0x68 => OP_68(),
                0x69 => OP_69(),
                0x6A => OP_6A(),
                0x6B => OP_6B(),
                0x6C => OP_6C(),
                0x6D => OP_6D(),
                0x6E => OP_6E(),
                0x6F => OP_6F(),
                0x70 => OP_70(),
                0x71 => OP_71(),
                0x72 => OP_72(),
                0x73 => OP_73(),
                0x74 => OP_74(),
                0x75 => OP_75(),
                0x76 => OP_76(),
                0x77 => OP_77(),
                0x78 => OP_78(),
                0x79 => OP_79(),
                0x7A => OP_7A(),
                0x7B => OP_7B(),
                0x7C => OP_7C(),
                0x7D => OP_7D(),
                0x7E => OP_7E(),
                0x7F => OP_7F(),
                0x80 => OP_80(),
                0x81 => OP_81(),
                0x82 => OP_82(),
                0x83 => OP_83(),
                0x84 => OP_84(),
                0x85 => OP_85(),
                0x86 => OP_86(),
                0x87 => OP_87(),
                0x88 => OP_88(),
                0x89 => OP_89(),
                0x8A => OP_8A(),
                0x8B => OP_8B(),
                0x8C => OP_8C(),
                0x8D => OP_8D(),
                0x8E => OP_8E(),
                0x8F => OP_8F(),
                0x90 => OP_90(),
                0x91 => OP_91(),
                0x92 => OP_92(),
                0x93 => OP_93(),
                0x94 => OP_94(),
                0x95 => OP_95(),
                0x96 => OP_96(),
                0x97 => OP_97(),
                0x98 => OP_98(),
                0x99 => OP_99(),
                0x9A => OP_9A(),
                0x9B => OP_9B(),
                0x9C => OP_9C(),
                0x9D => OP_9D(),
                0x9E => OP_9E(),
                0x9F => OP_9F(),
                0xA0 => OP_A0(),
                0xA1 => OP_A1(),
                0xA2 => OP_A2(),
                0xA3 => OP_A3(),
                0xA4 => OP_A4(),
                0xA5 => OP_A5(),
                0xA6 => OP_A6(),
                0xA7 => OP_A7(),
                0xA8 => OP_A8(),
                0xA9 => OP_A9(),
                0xAA => OP_AA(),
                0xAB => OP_AB(),
                0xAC => OP_AC(),
                0xAD => OP_AD(),
                0xAE => OP_AE(),
                0xAF => OP_AF(),
                0xB0 => OP_B0(),
                0xB1 => OP_B1(),
                0xB2 => OP_B2(),
                0xB3 => OP_B3(),
                0xB4 => OP_B4(),
                0xB5 => OP_B5(),
                0xB6 => OP_B6(),
                0xB7 => OP_B7(),
                0xB8 => OP_B8(),
                0xB9 => OP_B9(),
                0xBA => OP_BA(),
                0xBB => OP_BB(),
                0xBC => OP_BC(),
                0xBD => OP_BD(),
                0xBE => OP_BE(),
                0xBF => OP_BF(),
                0xC0 => OP_C0(),
                0xC1 => OP_C1(),
                0xC2 => OP_C2(),
                0xC3 => OP_C3(),
                0xC4 => OP_C4(),
                0xC5 => OP_C5(),
                0xC6 => OP_C6(),
                0xC7 => OP_C7(),
                0xC8 => OP_C8(),
                0xC9 => OP_C9(),
                0xCA => OP_CA(),
                0xCC => OP_CC(),
                0xCD => OP_CD(),
                0xCE => OP_CE(),
                0xCF => OP_CF(),
                0xD0 => OP_D0(),
                0xD1 => OP_D1(),
                0xD2 => OP_D2(),
                0xD3 => OP_D3(),
                0xD4 => OP_D4(),
                0xD5 => OP_D5(),
                0xD6 => OP_D6(),
                0xD7 => OP_D7(),
                0xD8 => OP_D8(),
                0xDA => OP_DA(),
                0xDB => OP_DB(),
                0xDC => OP_DC(),
                0xDE => OP_DE(),
                0xDF => OP_DF(),
                0xE0 => OP_E0(),
                0xE1 => OP_E1(),
                0xE2 => OP_E2(),
                0xE3 => OP_E3(),
                0xE4 => OP_E4(),
                0xE5 => OP_E5(),
                0xE6 => OP_E6(),
                0xE7 => OP_E7(),
                0xE8 => OP_E8(),
                0xE9 => OP_E9(),
                0xEA => OP_EA(),
                0xEB => OP_EB(),
                0xEC => OP_EC(),
                0xEE => OP_EE(),
                0xEF => OP_EF(),
                0xF0 => OP_F0(),
                0xF1 => OP_F1(),
                0xF2 => OP_F2(),
                0xF3 => OP_F3(),
                0xF4 => OP_F4(),
                0xF5 => OP_F5(),
                0xF6 => OP_F6(),
                0xF7 => OP_F7(),
                0xF8 => OP_F8(),
                0xF9 => OP_F9(),
                0xFA => OP_FA(),
                0xFB => OP_FB(),
                0xFC => OP_FC(),
                0xFE => OP_FE(),
                0xFF => OP_FF(),
                _ => throw new NotImplementedException("INVALID OPCODE - " + opcode.ToString("X2")),
            };
        }

        private static int OP_00()
        {
            // NOP
            return 4;
        }

        private int OP_01()
        {
            _registers.C = _memory.ReadByte(_registers.PC + 1);
            _registers.B = _memory.ReadByte(_registers.PC + 2);
            _registers.PC += 2;
            return 10;
        }

        private int OP_02()
        {
            _memory.WriteByte(_registers.BC, _registers.A);
            return 7;
        }

        private int OP_03()
        {
            var addr = _registers.BC;
            addr++;
            _registers.BC = addr;
            return 5;
        }

        private int OP_04()
        {
            _registers.B++;
            _flags!.UpdateZSP(_registers.B);
            return 5;
        }

        private int OP_05()
        {
            _registers.B--;
            _flags!.UpdateZSP(_registers.B);
            return 5;
        }

        private int OP_06()
        {
            _registers.B = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        private int OP_07()
        {
            var bit7 = ((_registers.A & 0x80) == 0x80) ? 1 : 0;
            _registers.A = (byte)((_registers.A << 1) | bit7);
            _flags!.CY = (byte)bit7;
            return 4;
        }

        private int OP_09()
        {
            var addr = _registers.HL + _registers.BC;
            _flags!.UpdateCarryWord(addr);
            _registers.HL = addr & 0xFFFF;
            return 10;
        }

        private int OP_0A()
        {
            var addr = _registers.BC;
            _registers.A = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_0B()
        {
            var addr = _registers.BC;
            addr--;
            _registers.BC = addr;
            return 5;
        }

        private int OP_0C()
        {
            _registers.C++;
            _flags!.UpdateZSP(_registers.C);
            return 5;
        }

        private int OP_0D()
        {
            _registers.C--;
            _flags!.UpdateZSP(_registers.C);
            return 5;
        }

        private int OP_0E()
        {
            _registers.C = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        private int OP_0F()
        {
            var bit0 = _registers.A & 0x01;
            _registers.A >>= 1;
            _registers.A |= (byte)(bit0 << 7);
            _flags!.CY = (byte)bit0;
            return 4;
        }

        private int OP_11()
        {
            _registers.D = _memory.ReadByte(_registers.PC + 2);
            _registers.E = _memory.ReadByte(_registers.PC + 1);
            _registers.PC += 2;
            return 10;
        }

        private int OP_12()
        {
            var addr = _registers.DE;
            _memory.WriteByte(addr, _registers.A);
            return 7;
        }

        private int OP_13()
        {
            var addr = _registers.DE; ;
            addr++;
            _registers.DE = addr;
            return 5;
        }

        private int OP_14()
        {
            _registers.D++;
            _flags!.UpdateZSP(_registers.D);
            return 5;
        }

        private int OP_15()
        {
            _registers.D--;
            _flags!.UpdateZSP(_registers.D);
            return 5;
        }

        private int OP_16()
        {
            _registers.D = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        private int OP_17()
        {
            var bit7 = (uint)(((_registers.A & 128) == 128) ? 1 : 0);
            var bit0 = _flags!.CY;
            _registers.A = (byte)((uint)(_registers.A << 1) | bit0);
            _flags!.CY = bit7;
            return 4;
        }

        private int OP_19()
        {
            var addr = _registers.DE + _registers.HL;
            _flags!.UpdateCarryWord(addr);
            _registers.HL = addr & 0xFFFF;
            return 10;
        }

        private int OP_1A()
        {
            var addr = _registers.DE;
            _registers.A = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_1B()
        {
            var addr = (ushort)_registers.DE;
            addr--;
            _registers.DE = addr;
            return 5;
        }

        private int OP_1C()
        {
            _registers.E++;
            _flags!.UpdateZSP(_registers.E);
            return 5;
        }

        private int OP_1D()
        {
            _registers.E--;
            _flags!.UpdateZSP(_registers.E);
            return 5;
        }

        private int OP_1E()
        {
            _registers.E = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        private int OP_1F()
        {
            var bit0 = _registers.A & 1;
            var bit7 = _flags!.CY;
            _registers.A = (byte)((uint)(_registers.A >> 1) | (bit7 << 7));
            _flags!.CY = (byte)bit0;
            return 4;
        }

        private static int OP_20() // RIM	1		special
        { return 4; }

        private int OP_21()
        {
            _registers.H = _memory.ReadByte(_registers.PC + 2);
            _registers.L = _memory.ReadByte(_registers.PC + 1);
            _registers.PC += 2;
            return 10;
        }

        private int OP_22()
        {
            var addr = ReadOpcodeDataWord();
            _memory.WriteByte(addr, _registers.L);
            _memory.WriteByte((uint)addr + 1, _registers.H);
            _registers.PC += 2;
            return 16;
        }

        private int OP_23()
        {
            var addr = _registers.HL;
            addr++;
            _registers.HL = addr;
            return 5;
        }

        private int OP_24()
        {
            _registers.H++;
            _flags!.UpdateZSP(_registers.H);
            return 5;
        }

        private int OP_25()
        {
            _registers.H--;
            _flags!.UpdateZSP(_registers.H);
            return 5;
        }

        private int OP_26()
        {
            _registers.H = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        private int OP_27()
        {
            byte bits = (byte)(_registers.A & 0x0F);
            if ((_registers.A & 0x0F) > 9 || _flags!.AC == 1)
            {
                _registers.A += 6;
                if (bits + 0x06 > 0x10)
                    _flags!.AC = 0x01;
                else
                    _flags!.AC = 0x00;
            }
            else
                _flags!.AC = 0x00;
            if ((_registers.A & 0xF0) > 0x90 || _flags!.CY == 1)
            {
                ushort addr = (ushort)(_registers.A + 0x60);
                _flags!.UpdateZSP(addr);
                _flags!.UpdateCarryByte(addr);
                _registers.A = (byte)(addr & 0xFF);
            }
            else
                _flags!.CY = 0x00;
            return 4;
        }

        private int OP_29()
        {
            var addr = _registers.HL + _registers.HL;
            _flags!.UpdateCarryWord(addr);
            _registers.HL = addr & 0xFFFF;
            return 10;
        }

        private int OP_2A()
        {
            var addr = ReadOpcodeDataWord();
            _registers.L = _memory.ReadByte(addr);
            _registers.H = _memory.ReadByte((uint)addr + 1);
            _registers.PC += 2;
            return 16;
        }

        private int OP_2B()
        {
            var addr = _registers.HL;
            addr--;
            _registers.HL = addr;
            return 5;
        }

        private int OP_2C()
        {
            _registers.L++;
            _flags!.UpdateZSP(_registers.L);
            return 5;
        }

        private int OP_2D()
        {
            _registers.L--;
            _flags!.UpdateZSP(_registers.L);
            return 5;
        }

        private int OP_2E()
        {
            _registers.L = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        private int OP_2F()
        {
            _registers.A = (byte)~_registers.A;
            return 7;
        }

        private static int OP_30()  // SIM	1		special
        { return 4; }

        private int OP_31()
        {
            _registers.SP = ReadOpcodeDataWord();
            _registers.PC += 2;
            return 10;
        }

        private int OP_32()
        {
            ushort addr = ReadOpcodeDataWord();
            _memory.WriteByte(addr, _registers.A);
            _registers.PC += 2;
            return 15;
        }

        private int OP_33()
        {
            _registers.SP++;
            return 5;
        }

        private int OP_34()
        {
            var addr = _registers.HL;
            var value = _memory.ReadByte(addr);
            value++;
            _flags!.UpdateZSP(value);
            _memory.WriteByte(addr, (byte)(value & 0xFF));
            return 10;
        }

        private int OP_35()
        {
            var addr = _registers.HL;
            var value = _memory.ReadByte(addr);
            value--;
            _flags!.UpdateZSP(value);
            _memory.WriteByte(addr, (byte)(value & 0xFF));
            return 10;
        }

        private int OP_36()
        {
            var addr = _registers.HL;
            var value = _memory.ReadByte(_registers.PC + 1);
            _memory.WriteByte(addr, value);
            _registers.PC++;
            return 10;
        }

        private int OP_37()
        {
            _flags!.CY = 1;
            return 4;
        }

        private int OP_39()
        {
            var value = _registers.HL + _registers.SP;
            _flags!.UpdateCarryWord(value);
            _registers.HL = (value & 0xFFFF);
            return 10;
        }

        private int OP_3A()
        {
            var addr = ReadOpcodeDataWord();
            _registers.A = _memory.ReadByte(addr);
            _registers.PC += 2;
            return 13;
        }

        private int OP_3B()
        {
            _registers.SP--;
            return 5;
        }

        private int OP_3C()
        {
            _registers.A++;
            _flags!.UpdateZSP(_registers.A);
            return 5;
        }

        private int OP_3D()
        {
            _registers.A--;
            _flags!.UpdateZSP(_registers.A);
            return 5;
        }

        private int OP_3E()
        {
            var addr = _memory.ReadByte(_registers.PC + 1);
            _registers.A = addr;
            _registers.PC++;
            return 7;
        }

        private int OP_3F()
        {
            _flags!.CY = (byte)~_flags!.CY;
            return 4;
        }

        private int OP_40()
        {
            _registers.B = _registers.B;
            return 5;
        }

        private int OP_41()
        {
            _registers.B = _registers.C;
            return 5;
        }

        private int OP_42()
        {
            _registers.B = _registers.D;
            return 5;
        }

        private int OP_43()
        {
            _registers.B = _registers.E;
            return 5;
        }

        private int OP_44()
        {
            _registers.B = _registers.H;
            return 5;
        }

        private int OP_45()
        {
            _registers.B = _registers.L;
            return 5;
        }

        private int OP_46()
        {
            var addr = _registers.HL;
            _registers.B = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_47()
        {
            _registers.B = _registers.A;
            return 5;
        }

        private int OP_48()
        {
            _registers.C = _registers.B;
            return 5;
        }

        private int OP_49()
        {
            _registers.C = _registers.C;
            return 5;
        }

        private int OP_4A()
        {
            _registers.C = _registers.D;
            return 5;
        }

        private int OP_4B()
        {
            _registers.C = _registers.E;
            return 5;
        }

        private int OP_4C()
        {
            _registers.C = _registers.H;
            return 5;
        }

        private int OP_4D()
        {
            _registers.C = _registers.L;
            return 5;
        }

        private int OP_4E()
        {
            var addr = _registers.HL;
            _registers.C = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_4F()
        {
            _registers.C = _registers.A;
            return 5;
        }

        private int OP_50()
        {
            _registers.D = _registers.B;
            return 5;
        }

        private int OP_51()
        {
            _registers.D = _registers.C;
            return 5;
        }

        private int OP_52()
        {
            _registers.D = _registers.D;
            return 5;
        }

        private int OP_53()
        {
            _registers.D = _registers.E;
            return 5;
        }

        private int OP_54()
        {
            _registers.D = _registers.H;
            return 5;
        }

        private int OP_55()
        {
            _registers.D = _registers.L;
            return 5;
        }

        private int OP_56()
        {
            var addr = _registers.HL;
            _registers.D = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_57()
        {
            _registers.D = _registers.A;
            return 5;
        }

        private int OP_58()
        {
            _registers.E = _registers.B;
            return 5;
        }

        private int OP_59()
        {
            _registers.E = _registers.C;
            return 5;
        }

        private int OP_5A()
        {
            _registers.E = _registers.D;
            return 5;
        }

        private int OP_5B()
        {
            _registers.E = _registers.E;
            return 5;
        }

        private int OP_5C()
        {
            _registers.E = _registers.H;
            return 5;
        }

        private int OP_5D()
        {
            _registers.E = _registers.L;
            return 5;
        }

        private int OP_5E()
        {
            var addr = _registers.HL;
            _registers.E = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_5F()
        {
            _registers.E = _registers.A;
            return 5;
        }

        private int OP_60()
        {
            _registers.H = _registers.B;
            return 5;
        }

        private int OP_61()
        {
            _registers.H = _registers.C;
            return 5;
        }

        private int OP_62()
        {
            _registers.H = _registers.D;
            return 5;
        }

        private int OP_63()
        {
            _registers.H = _registers.E;
            return 5;
        }

        private int OP_64()
        {
            _registers.H = _registers.H;
            return 5;
        }

        private int OP_65()
        {
            _registers.H = _registers.L;
            return 5;
        }

        private int OP_66()
        {
            var addr = _registers.HL;
            _registers.H = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_67()
        {
            _registers.H = _registers.A;
            return 5;
        }

        private int OP_68()
        {
            _registers.L = _registers.B;
            return 5;
        }

        private int OP_69()
        {
            _registers.L = _registers.C;
            return 5;
        }

        private int OP_6A()
        {
            _registers.L = _registers.D;
            return 5;
        }

        private int OP_6B()
        {
            _registers.L = _registers.E;
            return 5;
        }

        private int OP_6C()
        {
            _registers.L = _registers.H;
            return 5;
        }

        private int OP_6D()
        {
            _registers.L = _registers.L;
            return 5;
        }

        private int OP_6E()
        {
            var addr = _registers.HL;
            _registers.L = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_6F()
        {
            _registers.L = _registers.A;
            return 5;
        }

        private int OP_70()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.B);
            return 7;
        }

        private int OP_71()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.C);
            return 7;
        }

        private int OP_72()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.D);
            return 7;
        }

        private int OP_73()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.E);
            return 7;
        }

        private int OP_74()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.H);
            return 7;
        }

        private int OP_75()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.L);
            return 7;
        }

        private int OP_76()
        {
            _running = false;
            return 7;
        }

        private int OP_77()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.A);
            return 7;
        }

        private int OP_78()
        {
            _registers.A = _registers.B;
            return 5;
        }

        private int OP_79()
        {
            _registers.A = _registers.C;
            return 5;
        }

        private int OP_7A()
        {
            _registers.A = _registers.D;
            return 5;
        }

        private int OP_7B()
        {
            _registers.A = _registers.E;
            return 5;
        }

        private int OP_7C()
        {
            _registers.A = _registers.H;
            return 5;
        }

        private int OP_7D()
        {
            _registers.A = _registers.L;
            return 5;
        }

        private int OP_7E()
        {
            var addr = _registers.HL;
            _registers.A = _memory.ReadByte(addr);
            return 7;
        }

        private int OP_7F()
        {
            _registers.A = _registers.A;
            return 5;
        }

        private int OP_80()
        {
            var addr = (uint)_registers.A + (uint)_registers.B;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.B, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.B);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_81()
        {
            var addr = (uint)_registers.A + (uint)_registers.C;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.C, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.C);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_82()
        {
            var addr = (uint)_registers.A + (uint)_registers.D;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.D, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.D);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_83()
        {
            var addr = (uint)_registers.A + (uint)_registers.E;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.E, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.E);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_84()
        {
            var addr = (uint)_registers.A + (uint)_registers.H;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.H, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.H);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_85()
        {
            var addr = (uint)_registers.A + (uint)_registers.L;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.L, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.L);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_86()
        {
            uint addr = (uint)_registers.A + _memory.ReadByte(_registers.HL);
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.HL), 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_87()
        {
            var addr = (uint)_registers.A + (uint)_registers.A;
            _flags!.UpdateAuxCarryFlag(_registers.A, _registers.A);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_88()
        {
            var addr = (uint)_registers.A + (uint)_registers.B + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.B, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.B);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_89()
        {
            var addr = (uint)_registers.A + (uint)_registers.C + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.C, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.C);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8A()
        {
            var addr = (uint)_registers.A + (uint)_registers.D + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.D, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.D);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8B()
        {
            var addr = (uint)_registers.A + (uint)_registers.E + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.E, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.E);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8C()
        {
            var addr = (uint)_registers.A + (uint)_registers.H + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.H, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.H);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8D()
        {
            var addr = (uint)_registers.A + (uint)_registers.L + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.L, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.L);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8E()
        {
            var addr = (uint)_registers.A + _memory.ReadByte(_registers.HL) + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.HL), 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_8F()
        {
            var addr = (uint)_registers.A + (uint)_registers.A + (uint)_flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.A, 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _registers.A);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_90()
        {
            uint reg = _registers.B;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));

            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_91()
        {
            uint reg = _registers.C;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));

            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_92()
        {
            uint reg = _registers.D;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));

            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_93()
        {
            uint reg = _registers.E;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));

            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_94()
        {
            uint reg = _registers.H;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_95()
        {
            uint reg = _registers.L;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_96()
        {
            uint reg = _memory.ReadByte(_registers.HL);
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_97()
        {
            uint reg = _registers.A;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_98()
        {
            uint reg = _registers.B;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_99()
        {
            uint reg = _registers.C;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9A()
        {
            uint reg = _registers.D;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9B()
        {
            uint reg = _registers.E;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9C()
        {
            uint reg = _registers.H;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9D()
        {
            uint reg = _registers.L;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9E()
        {
            uint reg = _memory.ReadByte(_registers.HL);
            if (_flags!.CY == 1)
                reg += 1;
            UInt16 addr = (UInt16)(_registers.A + (byte)(~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_9F()
        {
            uint reg = (uint)_registers.A;
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_A0()
        {
            _registers.A = (byte)(_registers.A & _registers.B);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A1()
        {
            _registers.A = (byte)(_registers.A & _registers.C);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A2()
        {
            _registers.A = (byte)(_registers.A & _registers.D);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A3()
        {
            _registers.A = (byte)(_registers.A & _registers.E);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A4()
        {
            _registers.A = (byte)(_registers.A & _registers.H);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A5()
        {
            _registers.A = (byte)(_registers.A & _registers.L);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A6()
        {
            _registers.A = (byte)(_registers.A & _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 7;
        }

        private int OP_A7()
        {
            _registers.A = (byte)(_registers.A & _registers.A);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A8()
        {
            _registers.A = (byte)(_registers.A ^ _registers.B);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_A9()
        {
            _registers.A = (byte)(_registers.A ^ _registers.C);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_AA()
        {
            _registers.A = (byte)(_registers.A ^ _registers.D);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_AB()
        {
            _registers.A = (byte)(_registers.A ^ _registers.E);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_AC()
        {
            _registers.A = (byte)(_registers.A ^ _registers.H);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_AD()
        {
            _registers.A = (byte)(_registers.A ^ _registers.L);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_AE()
        {
            _registers.A = (byte)(_registers.A ^ _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 7;
        }

        private int OP_AF()
        {
            _registers.A = (byte)(_registers.A ^ _registers.A);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B0()
        {
            _registers.A = (byte)(_registers.A | _registers.B);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B1()
        {
            _registers.A = (byte)(_registers.A | _registers.C);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B2()
        {
            _registers.A = (byte)(_registers.A | _registers.D);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B3()
        {
            _registers.A = (byte)(_registers.A | _registers.E);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B4()
        {
            _registers.A = (byte)(_registers.A | _registers.H);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B5()
        {
            _registers.A = (byte)(_registers.A | _registers.L);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B6()
        {
            _registers.A = (byte)(_registers.A | _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 7;
        }

        private int OP_B7()
        {
            _registers.A = (byte)(_registers.A | _registers.A);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        private int OP_B8()
        {
            var addr = (byte)(_registers.A - _registers.B);
            _flags!.UpdateZSP(addr);
            if (_registers.A < _registers.B)
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 4;
        }

        private int OP_B9()
        {
            var addr = (byte)(_registers.A - _registers.C);
            _flags!.UpdateZSP(addr);
            if (_registers.A < _registers.C)
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 4;
        }

        private int OP_BA()
        {
            var addr = (byte)(_registers.A - _registers.D);
            _flags!.UpdateZSP(addr);
            if (_registers.A < _registers.D)
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 4;
        }

        private int OP_BB()
        {
            var addr = (byte)(_registers.A - _registers.E);
            _flags!.UpdateZSP(addr);
            if (_registers.A < _registers.E)
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 4;
        }

        private int OP_BC()
        {
            var addr = (byte)(_registers.A - _registers.H);
            _flags!.UpdateZSP(addr);
            if (_registers.A < _registers.H)
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 4;
        }

        private int OP_BD()
        {
            var addr = (byte)(_registers.A - _registers.L);
            _flags!.UpdateZSP(addr);
            if (_registers.A < _registers.L)
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 4;
        }

        private int OP_BE()
        {
            var addr = (byte)(_registers.A - _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(addr);
            if (_registers.A < _memory.ReadByte(_registers.HL))
                _flags!.CY = 1;
            else
                _flags!.CY = 0;
            return 7;
        }

        private int OP_BF()
        {
            _flags!.Z = 1;
            _flags!.S = 0;
            _flags!.P = Flags.CalculateParityFlag(_registers.A);
            _flags!.CY = 0;
            return 4;
        }

        private int OP_C0()
        {
            if (_flags!.Z == 0)
            {
                _registers.PC = (uint)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_C1()
        {
            _registers.C = _memory.ReadByte(_registers.SP);
            _registers.B = _memory.ReadByte(_registers.SP + 1);
            _registers.SP += 2;
            return 10;
        }

        private int OP_C2()
        {
            if (_flags!.Z == 0)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_C3()
        {
            var addr = ReadOpcodeDataWord();
            _registers.PC = addr;
            _registers.PC--;
            return 10;
        }

        private int OP_C4()
        {
            if (_flags!.Z == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(_registers.PC + 3);
                Call(addr, retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_C5()
        {
            _memory.WriteByte(_registers.SP - 2, _registers.C);
            _memory.WriteByte(_registers.SP - 1, _registers.B);
            _registers.SP -= 2;
            return 11;
        }

        private int OP_C6()
        {
            var addr = (uint)_registers.A + _memory.ReadByte(_registers.PC + 1);
            _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.PC + 1));
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            _registers.PC++;
            return 7;
        }

        private int OP_C7()
        {
            Call(0x38, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_C8()
        {
            if (_flags!.Z == 1)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_C9()
        {
            _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
            _registers.SP += 2;
            _registers.PC--;
            return 10;
        }

        private int OP_CA()
        {
            if (_flags!.Z == 1)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_CC()
        {
            if (_flags!.Z == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ulong)(_registers.PC + 3);
                Call((ushort)addr, (ushort)retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_CD()
        {
            var addr = ReadOpcodeDataWord();
            var retAddr = (ulong)(_registers.PC + 3);
            Call((ushort)addr, (ushort)retAddr);
            _registers.PC--;
            return 17;
        }

        private int OP_CE()
        {
            uint addr = _registers.A;
            addr += _memory.ReadByte(_registers.PC + 1);
            addr += _flags!.CY;
            if (_flags!.CY == 1)
                _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.PC + 1), 1);
            else
                _flags!.UpdateAuxCarryFlag(_registers.A, _memory.ReadByte(_registers.PC + 1));
            _flags!.UpdateCarryByte(addr);
            _flags!.UpdateZSP(addr);
            _registers.A = (byte)(addr & 0xFF);
            _registers.PC++;
            return 7;
        }

        private int OP_CF()
        {
            Call(0x08, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_D0()
        {
            if (_flags!.CY == 0)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_D1()
        {
            _registers.E = _memory.ReadByte(_registers.SP);
            _registers.D = _memory.ReadByte(_registers.SP + 1);
            _registers.SP += 2;
            return 10;
        }

        private int OP_D2()
        {
            if (_flags!.CY == 0)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = (ushort)addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_D3()
        {
            var port = _memory.ReadByte(_registers.PC + 1);
            switch (port)
            {
                case 2:
                    _hardwareShiftRegisterOffset = _registers.A & 0x07;
                    break;

                case 3:
                    _portOut[3] = _registers.A;
                    break;

                case 4:
                    _hardwareShiftRegisterData = (_hardwareShiftRegisterData >> 8) | (_registers.A << 8);
                    break;

                case 5:
                    _portOut[5] = _registers.A;
                    break;
            }
            _registers.PC++;
            _soundTiming.Set();
            return 10;
        }

        private int OP_D4()
        {
            if (_flags!.CY == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ulong)(_registers.PC + 3);
                Call((ushort)addr, (ushort)retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_D5()
        {
            _memory.WriteByte(_registers.SP - 2, _registers.E);
            _memory.WriteByte(_registers.SP - 1, _registers.D);
            _registers.SP -= 2;
            return 11;
        }

        private int OP_D6()
        {
            uint data = _memory.ReadByte(_registers.PC + 1);
            uint addr = _registers.A - data;
            _flags!.UpdateCarryByte(addr);
            _flags!.UpdateZSP(addr);
            _registers.A = (byte)(addr & 0xFF);
            _registers.PC++;
            return 7;
        }

        private int OP_D7()
        {
            Call(0x10, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_D8()
        {
            if (_flags!.CY == 1)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_DA()
        {
            if (_flags!.CY == 1)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_DB()
        {
            var port = _memory.ReadByte(_registers.PC + 1);
            switch (port)
            {
                case 0:
                    _registers.A = _portIn[0];
                    break;

                case 1:
                    _registers.A = _portIn[1];
                    break;

                case 2:
                    _registers.A = _portIn[2];
                    break;

                case 3:
                    _registers.A = (byte)(_hardwareShiftRegisterData >> (8 - _hardwareShiftRegisterOffset));
                    break;
            }
            _registers.PC++;
            return 10;
        }

        private int OP_DC()
        {
            if (_flags!.CY == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ulong)(_registers.PC + 3);
                Call(addr, (ushort)retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_DE()
        {
            uint data = _memory.ReadByte(_registers.PC + 1);
            uint addr = _registers.A - data - _flags!.CY;
            _flags!.UpdateCarryByte(addr);
            _flags!.UpdateZSP(addr);
            _registers.A = (byte)(addr & 0xFF);
            _registers.PC++;
            return 7;
        }

        private int OP_DF()
        {
            Call(0x18, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_E0()
        {
            if (_flags!.P == 0)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_E1()
        {
            _registers.L = _memory.ReadByte(_registers.SP);
            _registers.H = _memory.ReadByte(_registers.SP + 1);
            _registers.SP += 2;
            return 10;
        }

        private int OP_E2()
        {
            if (_flags!.P == 0)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_E3()
        {
            var l = _registers.L;
            var h = _registers.H;
            _registers.L = _memory.ReadByte(_registers.SP);
            _registers.H = _memory.ReadByte(_registers.SP + 1);
            _memory.WriteByte(_registers.SP, l);
            _memory.WriteByte(_registers.SP + 1, h);
            return 18;
        }

        private int OP_E4()
        {
            if (_flags!.P == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(_registers.PC + 3);
                Call(addr, retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_E5()
        {
            _memory.WriteByte(_registers.SP - 2, _registers.L);
            _memory.WriteByte(_registers.SP - 1, _registers.H);
            _registers.SP -= 2;
            return 11;
        }

        private int OP_E6()
        {
            uint addr = (uint)(_registers.A & _memory.ReadByte(_registers.PC + 1));
            _flags!.UpdateZSP(addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            _registers.PC++;
            return 7;
        }

        private int OP_E7()
        {
            Call(0x20, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_E8()
        {
            if (_flags!.P == 1)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_E9()
        {
            _registers.PC = (ushort)(_registers.H << 8 | _registers.L);
            _registers.PC--;
            return 5;
        }

        private int OP_EA()
        {
            if (_flags!.P == 1)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_EB()
        {
            (_registers.D, _registers.H) = (_registers.H, _registers.D);
            (_registers.L, _registers.E) = (_registers.E, _registers.L);
            return 5;
        }

        private int OP_EC()
        {
            if (_flags!.P == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(_registers.PC + 3);
                Call(addr, retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_EE()
        {
            _registers.A ^= _memory.ReadByte(_registers.PC + 1);
            _flags!.UpdateCarryByte(_registers.A);
            _flags!.UpdateZSP(_registers.A);
            _registers.PC++;
            return 7;
        }

        private int OP_EF()
        {
            Call(0x28, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_F0()
        {
            if (_flags!.P == 1)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_F1()
        {
            _registers.A = _memory.ReadByte(_registers.SP + 1);
            var local_flags = _memory.ReadByte(_registers.SP);
            if (0x01 == (local_flags & 0x01)) _flags!.Z = 0x01; else _flags!.Z = 0x00;
            if (0x02 == (local_flags & 0x02)) _flags!.S = 0x01; else _flags!.S = 0x00;
            if (0x04 == (local_flags & 0x04)) _flags!.P = 0x01; else _flags!.P = 0x00;
            if (0x08 == (local_flags & 0x08)) _flags!.CY = 0x01; else _flags!.CY = 0x00;
            if (0x10 == (local_flags & 0x10)) _flags!.AC = 0x01; else _flags!.AC = 0x00;
            _registers.SP += 2;
            return 10;
        }

        private int OP_F2()
        {
            if (_flags!.P == 1)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_F3()
        {
            _registers.IntEnable = false;
            return 4;
        }

        private int OP_F4()
        {
            if (_flags!.S == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(_registers.PC + 3);
                Call(addr, retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_F5()
        {
            _memory.WriteByte(_registers.SP - 1, _registers.A);
            byte addr = ((byte)(_flags!.Z | _flags!.S << 1 | _flags!.P << 2 | _flags!.CY << 3 | _flags!.AC << 4));
            _memory.WriteByte(_registers.SP - 2, addr);
            _registers.SP -= 2;
            return 11;
        }

        private int OP_F6()
        {
            uint data = _memory.ReadByte(_registers.PC + 1);
            uint value = _registers.A | data;
            _flags!.UpdateCarryByte(value);
            _flags!.UpdateZSP(value);
            _registers.A = (byte)value;
            _registers.PC++;
            return 7;
        }

        private int OP_F7()
        {
            Call(0x30, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        private int OP_F8()
        {
            if (_flags!.S == 1)
            {
                _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
                _registers.SP += 2;
                _registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_F9()
        {
            _registers.SP = (ushort)_registers.HL;
            return 5;
        }

        private int OP_FA()
        {
            if (_flags!.S == 1)
            {
                var addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        private int OP_FB()
        {
            _registers.IntEnable = true;
            return 4;
        }

        private int OP_FC()
        {
            if (_flags!.S == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(_registers.PC + 3);
                Call(addr, retAddr);
                _registers.PC--;
                return 17;
            }
            else
            {
                _registers.PC += 2;
            }
            return 11;
        }

        private int OP_FE()
        {
            UInt16 addr = (UInt16)(_registers.A + (byte)(~(_memory.ReadByte(_registers.PC + 1)) & 0xFF) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP(addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)(~(_memory.ReadByte(_registers.PC + 1)) & 0xFF), 1);
            _registers.PC++;
            return 7;
        }

        private int OP_FF()
        {
            Call(0x38, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }
    }
}
