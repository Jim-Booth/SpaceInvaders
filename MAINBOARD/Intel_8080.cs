
﻿using System.Diagnostics;

namespace Invaders.MAINBOARD
{
    internal class Intel_8080
    {
        private bool running = false;

        public bool Running
        {
            get { return running; }
            set { running = value; }
        }

        private byte[] portIn = new byte[4]; // 0,1,2,3

        public byte[] PortIn
        {
            set { portIn = value; }
            get { return portIn; }
        }

        private readonly byte[] portOut = new byte[7]; // 2,3,5,6

        public byte[] PortOut
        {
            get { return portOut; }
        }

        private readonly Memory memory;

        public Memory Memory
        {
            get { return memory; }
        }

        private readonly byte[] video;

        public byte[] Video
        {
            get { return video; }
        }

        private readonly AutoResetEvent displayTiming = new(false);

        public AutoResetEvent DisplayTiming
        {
            get { return displayTiming; }
        }

        private readonly AutoResetEvent soundTiming = new(false);

        public AutoResetEvent SoundTiming
        {
            get { return soundTiming; }
        }

        private readonly Registers registers = new();
        private readonly Flags flags = new();
        private readonly uint videoStartAddress;
        private int hardwareShiftRegisterData = 0;
        private int hardwareShiftRegisterOffset = 0;
        private static readonly int CLOCK_SPEED = 2000000; // 2 Mhz
        private static readonly int FREQUENCY = 60; //60 Hz
        private static readonly int HALF_FRAME_CYCLES_MAX = (CLOCK_SPEED / FREQUENCY) / 2;// 2,000,000/60 = 33,333/2 = 16,666
        private readonly int FRAME_TIME_MS = 1000 / FREQUENCY; // 1/60 = 16.7ms
        public int FrameTiming
        { get { return FRAME_TIME_MS; } }
        private readonly Stopwatch frameTiming = new();

        public Intel_8080(Memory memory)
        {
            this.memory = memory;
            video = new byte[0x1C00];
            videoStartAddress = 0x2400;
            registers.PC = 0x0000;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            running = true;
            while (running && !cancellationToken.IsCancellationRequested)
            {
                frameTiming.Restart();// frame start
                ExecuteCycles(HALF_FRAME_CYCLES_MAX);// 1st half of frame
                Interrupt(1);// mid screen Interrupt
                ExecuteCycles(HALF_FRAME_CYCLES_MAX); // 2nd half of frame
                Interrupt(2);// full screen interrupt
                Buffer.BlockCopy(memory.GetMemory, (int)videoStartAddress, video, 0, video.Length); // draw the video
                displayTiming.Set();// signal the display to draw (non blocking)
                int mS = (FRAME_TIME_MS - (int)frameTiming.ElapsedMilliseconds);
                if (mS > 0)
                    await Task.Delay(mS, cancellationToken);
            }
        }

        private void ExecuteCycles(int maxCycles)
        {
            int cycles = 0;
            while (running && cycles < maxCycles)
            {
                byte opcode = memory.ReadByte(registers.PC);
                cycles += CallOpcode(opcode);
                registers.PC++;
            }
        }

        public void Stop()
        {
            running = false;
        }

        private ushort ReadOpcodeDataWord()
        {
            return (ushort)(memory.ReadByte(registers.PC + 2) << 8 | memory.ReadByte(registers.PC + 1));
        }

        private void Call(ushort address, ushort retAddress)
        {
            memory.WriteByte(registers.SP - 1, (byte)((retAddress >> 8) & 0xFF));
            memory.WriteByte(registers.SP - 2, (byte)(retAddress & 0xFF));
            registers.PC = address;
            registers.SP -= 2;
        }

        private void Interrupt(int addr)
        {
            if (registers.INT_ENABLE)
            {
                memory.WriteByte(registers.SP - 1, (byte)((registers.PC >> 8) & 0xFF));
                memory.WriteByte(registers.SP - 2, (byte)(registers.PC & 0xFF));
                registers.SP -= 2;
                if (addr == 1)
                    registers.PC = 0x0008;
                if (addr == 2)
                    registers.PC = 0x0010;
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
            registers.C = memory.ReadByte(registers.PC + 1);
            registers.B = memory.ReadByte(registers.PC + 2);
            registers.PC += 2;
            return 10;
        }

        private int OP_02()
        {
            memory.WriteByte(registers.BC, registers.A);
            return 7;
        }

        private int OP_03()
        {
            var addr = registers.BC;
            addr++;
            registers.BC = addr;
            return 5;
        }

        private int OP_04()
        {
            registers.B++;
            flags!.UpdateZSP(registers.B);
            return 5;
        }

        private int OP_05()
        {
            registers.B--;
            flags!.UpdateZSP(registers.B);
            return 5;
        }

        private int OP_06()
        {
            registers.B = memory.ReadByte(registers.PC + 1);
            registers.PC++;
            return 7;
        }

        private int OP_07()
        {
            var bit7 = ((registers.A & 0x80) == 0x80) ? 1 : 0;
            registers.A = (byte)((registers.A << 1) | bit7);
            flags!.CY = (byte)bit7;
            return 4;
        }

        private int OP_09()
        {
            var addr = registers.HL + registers.BC;
            flags!.UpdateCarryWord(addr);
            registers.HL = addr & 0xFFFF;
            return 10;
        }

        private int OP_0A()
        {
            var addr = registers.BC;
            registers.A = memory.ReadByte(addr);
            return 7;
        }

        private int OP_0B()
        {
            var addr = registers.BC;
            addr--;
            registers.BC = addr;
            return 5;
        }

        private int OP_0C()
        {
            registers.C++;
            flags!.UpdateZSP(registers.C);
            return 5;
        }

        private int OP_0D()
        {
            registers.C--;
            flags!.UpdateZSP(registers.C);
            return 5;
        }

        private int OP_0E()
        {
            registers.C = memory.ReadByte(registers.PC + 1);
            registers.PC++;
            return 7;
        }

        private int OP_0F()
        {
            var bit0 = registers.A & 0x01;
            registers.A >>= 1;
            registers.A |= (byte)(bit0 << 7);
            flags!.CY = (byte)bit0;
            return 4;
        }

        private int OP_11()
        {
            registers.D = memory.ReadByte(registers.PC + 2);
            registers.E = memory.ReadByte(registers.PC + 1);
            registers.PC += 2;
            return 10;
        }

        private int OP_12()
        {
            var addr = registers.DE;
            memory.WriteByte(addr, registers.A);
            return 7;
        }

        private int OP_13()
        {
            var addr = registers.DE; ;
            addr++;
            registers.DE = addr;
            return 5;
        }

        private int OP_14()
        {
            registers.D++;
            flags!.UpdateZSP(registers.D);
            return 5;
        }

        private int OP_15()
        {
            registers.D--;
            flags!.UpdateZSP(registers.D);
            return 5;
        }

        private int OP_16()
        {
            registers.D = memory.ReadByte(registers.PC + 1);
            registers.PC++;
            return 7;
        }

        private int OP_17()
        {
            var bit7 = (uint)(((registers.A & 128) == 128) ? 1 : 0);
            var bit0 = flags!.CY;
            registers.A = (byte)((uint)(registers.A << 1) | bit0);
            flags!.CY = bit7;
            return 4;
        }

        private int OP_19()
        {
            var addr = registers.DE + registers.HL;
            flags!.UpdateCarryWord(addr);
            registers.HL = addr & 0xFFFF;
            return 10;
        }

        private int OP_1A()
        {
            var addr = registers.DE;
            registers.A = memory.ReadByte(addr);
            return 7;
        }

        private int OP_1B()
        {
            var addr = (ushort)registers.DE;
            addr--;
            registers.DE = addr;
            return 5;
        }

        private int OP_1C()
        {
            registers.E++;
            flags!.UpdateZSP(registers.E);
            return 5;
        }

        private int OP_1D()
        {
            registers.E--;
            flags!.UpdateZSP(registers.E);
            return 5;
        }

        private int OP_1E()
        {
            registers.E = memory.ReadByte(registers.PC + 1);
            registers.PC++;
            return 7;
        }

        private int OP_1F()
        {
            var bit0 = registers.A & 1;
            var bit7 = flags!.CY;
            registers.A = (byte)((uint)(registers.A >> 1) | (bit7 << 7));
            flags!.CY = (byte)bit0;
            return 4;
        }

        private static int OP_20() // RIM	1		special
        { return 4; }

        private int OP_21()
        {
            registers.H = memory.ReadByte(registers.PC + 2);
            registers.L = memory.ReadByte(registers.PC + 1);
            registers.PC += 2;
            return 10;
        }

        private int OP_22()
        {
            var addr = ReadOpcodeDataWord();
            memory.WriteByte(addr, registers.L);
            memory.WriteByte((uint)addr + 1, registers.H);
            registers.PC += 2;
            return 16;
        }

        private int OP_23()
        {
            var addr = registers.HL;
            addr++;
            registers.HL = addr;
            return 5;
        }

        private int OP_24()
        {
            registers.H++;
            flags!.UpdateZSP(registers.H);
            return 5;
        }

        private int OP_25()
        {
            registers.H--;
            flags!.UpdateZSP(registers.H);
            return 5;
        }

        private int OP_26()
        {
            registers.H = memory.ReadByte(registers.PC + 1);
            registers.PC++;
            return 7;
        }

        private int OP_27()
        {
            byte bits = (byte)(registers.A & 0x0F);
            if ((registers.A & 0x0F) > 9 || flags!.AC == 1)
            {
                registers.A += 6;
                if (bits + 0x06 > 0x10)
                    flags!.AC = 0x01;
                else
                    flags!.AC = 0x00;
            }
            else
                flags!.AC = 0x00;
            if ((registers.A & 0xF0) > 0x90 || flags!.CY == 1)
            {
                ushort addr = (ushort)(registers.A + 0x60);
                flags!.UpdateZSP(addr);
                flags!.UpdateCarryByte(addr);
                registers.A = (byte)(addr & 0xFF);
            }
            else
                flags!.CY = 0x00;
            return 4;
        }

        private int OP_29()
        {
            var addr = registers.HL + registers.HL;
            flags!.UpdateCarryWord(addr);
            registers.HL = addr & 0xFFFF;
            return 10;
        }

        private int OP_2A()
        {
            var addr = ReadOpcodeDataWord();
            registers.L = memory.ReadByte(addr);
            registers.H = memory.ReadByte((uint)addr + 1);
            registers.PC += 2;
            return 16;
        }

        private int OP_2B()
        {
            var addr = registers.HL;
            addr--;
            registers.HL = addr;
            return 5;
        }

        private int OP_2C()
        {
            registers.L++;
            flags!.UpdateZSP(registers.L);
            return 5;
        }

        private int OP_2D()
        {
            registers.L--;
            flags!.UpdateZSP(registers.L);
            return 5;
        }

        private int OP_2E()
        {
            registers.L = memory.ReadByte(registers.PC + 1);
            registers.PC++;
            return 7;
        }

        private int OP_2F()
        {
            registers.A = (byte)~registers.A;
            return 7;
        }

        private static int OP_30()  // SIM	1		special
        { return 4; }

        private int OP_31()
        {
            registers.SP = ReadOpcodeDataWord();
            registers.PC += 2;
            return 10;
        }

        private int OP_32()
        {
            ushort addr = ReadOpcodeDataWord();
            memory.WriteByte(addr, registers.A);
            registers.PC += 2;
            return 15;
        }

        private int OP_33()
        {
            registers.SP++;
            return 5;
        }

        private int OP_34()
        {
            var addr = registers.HL;
            var value = memory.ReadByte(addr);
            value++;
            flags!.UpdateZSP(value);
            memory.WriteByte(addr, (byte)(value & 0xFF));
            return 10;
        }

        private int OP_35()
        {
            var addr = registers.HL;
            var value = memory.ReadByte(addr);
            value--;
            flags!.UpdateZSP(value);
            memory.WriteByte(addr, (byte)(value & 0xFF));
            return 10;
        }

        private int OP_36()
        {
            var addr = registers.HL;
            var value = memory.ReadByte(registers.PC + 1);
            memory.WriteByte(addr, value);
            registers.PC++;
            return 10;
        }

        private int OP_37()
        {
            flags!.CY = 1;
            return 4;
        }

        private int OP_39()
        {
            var value = registers.HL + registers.SP;
            flags!.UpdateCarryWord(value);
            registers.HL = (value & 0xFFFF);
            return 10;
        }

        private int OP_3A()
        {
            var addr = ReadOpcodeDataWord();
            registers.A = memory.ReadByte(addr);
            registers.PC += 2;
            return 13;
        }

        private int OP_3B()
        {
            registers.SP--;
            return 5;
        }

        private int OP_3C()
        {
            registers.A++;
            flags!.UpdateZSP(registers.A);
            return 5;
        }

        private int OP_3D()
        {
            registers.A--;
            flags!.UpdateZSP(registers.A);
            return 5;
        }

        private int OP_3E()
        {
            var addr = memory.ReadByte(registers.PC + 1);
            registers.A = addr;
            registers.PC++;
            return 7;
        }

        private int OP_3F()
        {
            flags!.CY = (byte)~flags!.CY;
            return 4;
        }

        private int OP_40()
        {
            registers.B = registers.B;
            return 5;
        }

        private int OP_41()
        {
            registers.B = registers.C;
            return 5;
        }

        private int OP_42()
        {
            registers.B = registers.D;
            return 5;
        }

        private int OP_43()
        {
            registers.B = registers.E;
            return 5;
        }

        private int OP_44()
        {
            registers.B = registers.H;
            return 5;
        }

        private int OP_45()
        {
            registers.B = registers.L;
            return 5;
        }

        private int OP_46()
        {
            var addr = registers.HL;
            registers.B = memory.ReadByte(addr);
            return 7;
        }

        private int OP_47()
        {
            registers.B = registers.A;
            return 5;
        }

        private int OP_48()
        {
            registers.C = registers.B;
            return 5;
        }

        private int OP_49()
        {
            registers.C = registers.C;
            return 5;
        }

        private int OP_4A()
        {
            registers.C = registers.D;
            return 5;
        }

        private int OP_4B()
        {
            registers.C = registers.E;
            return 5;
        }

        private int OP_4C()
        {
            registers.C = registers.H;
            return 5;
        }

        private int OP_4D()
        {
            registers.C = registers.L;
            return 5;
        }

        private int OP_4E()
        {
            var addr = registers.HL;
            registers.C = memory.ReadByte(addr);
            return 7;
        }

        private int OP_4F()
        {
            registers.C = registers.A;
            return 5;
        }

        private int OP_50()
        {
            registers.D = registers.B;
            return 5;
        }

        private int OP_51()
        {
            registers.D = registers.C;
            return 5;
        }

        private int OP_52()
        {
            registers.D = registers.D;
            return 5;
        }

        private int OP_53()
        {
            registers.D = registers.E;
            return 5;
        }

        private int OP_54()
        {
            registers.D = registers.H;
            return 5;
        }

        private int OP_55()
        {
            registers.D = registers.L;
            return 5;
        }

        private int OP_56()
        {
            var addr = registers.HL;
            registers.D = memory.ReadByte(addr);
            return 7;
        }

        private int OP_57()
        {
            registers.D = registers.A;
            return 5;
        }

        private int OP_58()
        {
            registers.E = registers.B;
            return 5;
        }

        private int OP_59()
        {
            registers.E = registers.C;
            return 5;
        }

        private int OP_5A()
        {
            registers.E = registers.D;
            return 5;
        }

        private int OP_5B()
        {
            registers.E = registers.E;
            return 5;
        }

        private int OP_5C()
        {
            registers.E = registers.H;
            return 5;
        }

        private int OP_5D()
        {
            registers.E = registers.L;
            return 5;
        }

        private int OP_5E()
        {
            var addr = registers.HL;
            registers.E = memory.ReadByte(addr);
            return 7;
        }

        private int OP_5F()
        {
            registers.E = registers.A;
            return 5;
        }

        private int OP_60()
        {
            registers.H = registers.B;
            return 5;
        }

        private int OP_61()
        {
            registers.H = registers.C;
            return 5;
        }

        private int OP_62()
        {
            registers.H = registers.D;
            return 5;
        }

        private int OP_63()
        {
            registers.H = registers.E;
            return 5;
        }

        private int OP_64()
        {
            registers.H = registers.H;
            return 5;
        }

        private int OP_65()
        {
            registers.H = registers.L;
            return 5;
        }

        private int OP_66()
        {
            var addr = registers.HL;
            registers.H = memory.ReadByte(addr);
            return 7;
        }

        private int OP_67()
        {
            registers.H = registers.A;
            return 5;
        }

        private int OP_68()
        {
            registers.L = registers.B;
            return 5;
        }

        private int OP_69()
        {
            registers.L = registers.C;
            return 5;
        }

        private int OP_6A()
        {
            registers.L = registers.D;
            return 5;
        }

        private int OP_6B()
        {
            registers.L = registers.E;
            return 5;
        }

        private int OP_6C()
        {
            registers.L = registers.H;
            return 5;
        }

        private int OP_6D()
        {
            registers.L = registers.L;
            return 5;
        }

        private int OP_6E()
        {
            var addr = registers.HL;
            registers.L = memory.ReadByte(addr);
            return 7;
        }

        private int OP_6F()
        {
            registers.L = registers.A;
            return 5;
        }

        private int OP_70()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.B);
            return 7;
        }

        private int OP_71()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.C);
            return 7;
        }

        private int OP_72()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.D);
            return 7;
        }

        private int OP_73()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.E);
            return 7;
        }

        private int OP_74()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.H);
            return 7;
        }

        private int OP_75()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.L);
            return 7;
        }

        private int OP_76()
        {
            running = false;
            return 7;
        }

        private int OP_77()
        {
            uint addr = registers.HL;
            memory.WriteByte(addr, registers.A);
            return 7;
        }

        private int OP_78()
        {
            registers.A = registers.B;
            return 5;
        }

        private int OP_79()
        {
            registers.A = registers.C;
            return 5;
        }

        private int OP_7A()
        {
            registers.A = registers.D;
            return 5;
        }

        private int OP_7B()
        {
            registers.A = registers.E;
            return 5;
        }

        private int OP_7C()
        {
            registers.A = registers.H;
            return 5;
        }

        private int OP_7D()
        {
            registers.A = registers.L;
            return 5;
        }

        private int OP_7E()
        {
            var addr = registers.HL;
            registers.A = memory.ReadByte(addr);
            return 7;
        }

        private int OP_7F()
        {
            registers.A = registers.A;
            return 5;
        }

        private int OP_80()
        {
            var addr = (uint)registers.A + (uint)registers.B;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.B, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.B);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_81()
        {
            var addr = (uint)registers.A + (uint)registers.C;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.C, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.C);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_82()
        {
            var addr = (uint)registers.A + (uint)registers.D;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.D, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.D);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_83()
        {
            var addr = (uint)registers.A + (uint)registers.E;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.E, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.E);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_84()
        {
            var addr = (uint)registers.A + (uint)registers.H;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.H, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.H);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);

            return 4;
        }

        private int OP_85()
        {
            var addr = (uint)registers.A + (uint)registers.L;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.L, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.L);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_86()
        {
            uint addr = (uint)registers.A + memory.ReadByte(registers.HL);
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.HL), 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.HL));
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_87()
        {
            var addr = (uint)registers.A + (uint)registers.A;
            flags!.UpdateAuxCarryFlag(registers.A, registers.A);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_88()
        {
            var addr = (uint)registers.A + (uint)registers.B + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.B, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.B);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_89()
        {
            var addr = (uint)registers.A + (uint)registers.C + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.C, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.C);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8A()
        {
            var addr = (uint)registers.A + (uint)registers.D + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.D, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.D);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8B()
        {
            var addr = (uint)registers.A + (uint)registers.E + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.E, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.E);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8C()
        {
            var addr = (uint)registers.A + (uint)registers.H + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.H, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.H);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8D()
        {
            var addr = (uint)registers.A + (uint)registers.L + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.L, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.L);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_8E()
        {
            var addr = (uint)registers.A + memory.ReadByte(registers.HL) + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.HL), 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.HL));
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_8F()
        {
            var addr = (uint)registers.A + (uint)registers.A + (uint)flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, registers.A, 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, registers.A);
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_90()
        {
            uint reg = registers.B;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));

            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_91()
        {
            uint reg = registers.C;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));

            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_92()
        {
            uint reg = registers.D;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));

            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_93()
        {
            uint reg = registers.E;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));

            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_94()
        {
            uint reg = registers.H;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_95()
        {
            uint reg = registers.L;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_96()
        {
            uint reg = memory.ReadByte(registers.HL);
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_97()
        {
            uint reg = registers.A;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_98()
        {
            uint reg = registers.B;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_99()
        {
            uint reg = registers.C;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9A()
        {
            uint reg = registers.D;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9B()
        {
            uint reg = registers.E;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9C()
        {
            uint reg = registers.H;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9D()
        {
            uint reg = registers.L;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_9E()
        {
            uint reg = memory.ReadByte(registers.HL);
            if (flags!.CY == 1)
                reg += 1;
            UInt16 addr = (UInt16)(registers.A + (byte)(~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        private int OP_9F()
        {
            uint reg = (uint)registers.A;
            if (flags!.CY == 1)
                reg += 1;
            var addr = (uint)(registers.A + (~reg & 0xff) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)((~reg & 0xff) & 0xFF));
            registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        private int OP_A0()
        {
            registers.A = (byte)(registers.A & registers.B);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A1()
        {
            registers.A = (byte)(registers.A & registers.C);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A2()
        {
            registers.A = (byte)(registers.A & registers.D);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A3()
        {
            registers.A = (byte)(registers.A & registers.E);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A4()
        {
            registers.A = (byte)(registers.A & registers.H);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A5()
        {
            registers.A = (byte)(registers.A & registers.L);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A6()
        {
            registers.A = (byte)(registers.A & memory.ReadByte(registers.HL));
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 7;
        }

        private int OP_A7()
        {
            registers.A = (byte)(registers.A & registers.A);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A8()
        {
            registers.A = (byte)(registers.A ^ registers.B);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_A9()
        {
            registers.A = (byte)(registers.A ^ registers.C);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_AA()
        {
            registers.A = (byte)(registers.A ^ registers.D);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_AB()
        {
            registers.A = (byte)(registers.A ^ registers.E);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_AC()
        {
            registers.A = (byte)(registers.A ^ registers.H);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_AD()
        {
            registers.A = (byte)(registers.A ^ registers.L);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_AE()
        {
            registers.A = (byte)(registers.A ^ memory.ReadByte(registers.HL));
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 7;
        }

        private int OP_AF()
        {
            registers.A = (byte)(registers.A ^ registers.A);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B0()
        {
            registers.A = (byte)(registers.A | registers.B);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B1()
        {
            registers.A = (byte)(registers.A | registers.C);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B2()
        {
            registers.A = (byte)(registers.A | registers.D);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B3()
        {
            registers.A = (byte)(registers.A | registers.E);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B4()
        {
            registers.A = (byte)(registers.A | registers.H);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B5()
        {
            registers.A = (byte)(registers.A | registers.L);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B6()
        {
            registers.A = (byte)(registers.A | memory.ReadByte(registers.HL));
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 7;
        }

        private int OP_B7()
        {
            registers.A = (byte)(registers.A | registers.A);
            flags!.UpdateZSP(registers.A);
            flags!.CY = 0;
            flags!.AC = 0;
            return 4;
        }

        private int OP_B8()
        {
            var addr = (byte)(registers.A - registers.B);
            flags!.UpdateZSP(addr);
            if (registers.A < registers.B)
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 4;
        }

        private int OP_B9()
        {
            var addr = (byte)(registers.A - registers.C);
            flags!.UpdateZSP(addr);
            if (registers.A < registers.C)
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 4;
        }

        private int OP_BA()
        {
            var addr = (byte)(registers.A - registers.D);
            flags!.UpdateZSP(addr);
            if (registers.A < registers.D)
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 4;
        }

        private int OP_BB()
        {
            var addr = (byte)(registers.A - registers.E);
            flags!.UpdateZSP(addr);
            if (registers.A < registers.E)
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 4;
        }

        private int OP_BC()
        {
            var addr = (byte)(registers.A - registers.H);
            flags!.UpdateZSP(addr);
            if (registers.A < registers.H)
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 4;
        }

        private int OP_BD()
        {
            var addr = (byte)(registers.A - registers.L);
            flags!.UpdateZSP(addr);
            if (registers.A < registers.L)
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 4;
        }

        private int OP_BE()
        {
            var addr = (byte)(registers.A - memory.ReadByte(registers.HL));
            flags!.UpdateZSP(addr);
            if (registers.A < memory.ReadByte(registers.HL))
                flags!.CY = 1;
            else
                flags!.CY = 0;
            return 7;
        }

        private int OP_BF()
        {
            flags!.Z = 1;
            flags!.S = 0;
            flags!.P = Flags.CalculateParityFlag(registers.A);
            flags!.CY = 0;
            return 4;
        }

        private int OP_C0()
        {
            if (flags!.Z == 0)
            {
                registers.PC = (uint)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_C1()
        {
            registers.C = memory.ReadByte(registers.SP);
            registers.B = memory.ReadByte(registers.SP + 1);
            registers.SP += 2;
            return 10;
        }

        private int OP_C2()
        {
            if (flags!.Z == 0)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_C3()
        {
            var addr = ReadOpcodeDataWord();
            registers.PC = addr;
            registers.PC--;
            return 10;
        }

        private int OP_C4()
        {
            if (flags!.Z == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(registers.PC + 3);
                Call(addr, retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_C5()
        {
            memory.WriteByte(registers.SP - 2, registers.C);
            memory.WriteByte(registers.SP - 1, registers.B);
            registers.SP -= 2;
            return 11;
        }

        private int OP_C6()
        {
            var addr = (uint)registers.A + memory.ReadByte(registers.PC + 1);
            flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.PC + 1));
            flags!.UpdateZSP((byte)addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            registers.PC++;
            return 7;
        }

        private int OP_C7()
        {
            Call(0x38, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_C8()
        {
            if (flags!.Z == 1)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_C9()
        {
            registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
            registers.SP += 2;
            registers.PC--;
            return 10;
        }

        private int OP_CA()
        {
            if (flags!.Z == 1)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_CC()
        {
            if (flags!.Z == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ulong)(registers.PC + 3);
                Call((ushort)addr, (ushort)retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_CD()
        {
            var addr = ReadOpcodeDataWord();
            var retAddr = (ulong)(registers.PC + 3);
            Call((ushort)addr, (ushort)retAddr);
            registers.PC--;
            return 17;
        }

        private int OP_CE()
        {
            uint addr = registers.A;
            addr += memory.ReadByte(registers.PC + 1);
            addr += flags!.CY;
            if (flags!.CY == 1)
                flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.PC + 1), 1);
            else
                flags!.UpdateAuxCarryFlag(registers.A, memory.ReadByte(registers.PC + 1));
            flags!.UpdateCarryByte(addr);
            flags!.UpdateZSP(addr);
            registers.A = (byte)(addr & 0xFF);
            registers.PC++;
            return 7;
        }

        private int OP_CF()
        {
            Call(0x08, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_D0()
        {
            if (flags!.CY == 0)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_D1()
        {
            registers.E = memory.ReadByte(registers.SP);
            registers.D = memory.ReadByte(registers.SP + 1);
            registers.SP += 2;
            return 10;
        }

        private int OP_D2()
        {
            if (flags!.CY == 0)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = (ushort)addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_D3()
        {
            var port = memory.ReadByte(registers.PC + 1);
            switch (port)
            {
                case 2:
                    hardwareShiftRegisterOffset = registers.A & 0x07;
                    break;

                case 3:
                    portOut[3] = registers.A;
                    break;

                case 4:
                    hardwareShiftRegisterData = (hardwareShiftRegisterData >> 8) | (registers.A << 8);
                    break;

                case 5:
                    portOut[5] = registers.A;
                    break;
            }
            registers.PC++;
            soundTiming.Set();
            return 10;
        }

        private int OP_D4()
        {
            if (flags!.CY == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ulong)(registers.PC + 3);
                Call((ushort)addr, (ushort)retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_D5()
        {
            memory.WriteByte(registers.SP - 2, registers.E);
            memory.WriteByte(registers.SP - 1, registers.D);
            registers.SP -= 2;
            return 11;
        }

        private int OP_D6()
        {
            uint data = memory.ReadByte(registers.PC + 1);
            uint addr = registers.A - data;
            flags!.UpdateCarryByte(addr);
            flags!.UpdateZSP(addr);
            registers.A = (byte)(addr & 0xFF);
            registers.PC++;
            return 7;
        }

        private int OP_D7()
        {
            Call(0x10, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_D8()
        {
            if (flags!.CY == 1)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_DA()
        {
            if (flags!.CY == 1)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_DB()
        {
            var port = memory.ReadByte(registers.PC + 1);
            switch (port)
            {
                case 0:
                    registers.A = portIn[0];
                    break;

                case 1:
                    registers.A = portIn[1];
                    break;

                case 2:
                    registers.A = portIn[2];
                    break;

                case 3:
                    registers.A = (byte)(hardwareShiftRegisterData >> (8 - hardwareShiftRegisterOffset));
                    break;
            }
            registers.PC++;
            return 10;
        }

        private int OP_DC()
        {
            if (flags!.CY == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ulong)(registers.PC + 3);
                Call(addr, (ushort)retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_DE()
        {
            uint data = memory.ReadByte(registers.PC + 1);
            uint addr = registers.A - data - flags!.CY;
            flags!.UpdateCarryByte(addr);
            flags!.UpdateZSP(addr);
            registers.A = (byte)(addr & 0xFF);
            registers.PC++;
            return 7;
        }

        private int OP_DF()
        {
            Call(0x18, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_E0()
        {
            if (flags!.P == 0)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_E1()
        {
            registers.L = memory.ReadByte(registers.SP);
            registers.H = memory.ReadByte(registers.SP + 1);
            registers.SP += 2;
            return 10;
        }

        private int OP_E2()
        {
            if (flags!.P == 0)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_E3()
        {
            var l = registers.L;
            var h = registers.H;
            registers.L = memory.ReadByte(registers.SP);
            registers.H = memory.ReadByte(registers.SP + 1);
            memory.WriteByte(registers.SP, l);
            memory.WriteByte(registers.SP + 1, h);
            return 18;
        }

        private int OP_E4()
        {
            if (flags!.P == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(registers.PC + 3);
                Call(addr, retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_E5()
        {
            memory.WriteByte(registers.SP - 2, registers.L);
            memory.WriteByte(registers.SP - 1, registers.H);
            registers.SP -= 2;
            return 11;
        }

        private int OP_E6()
        {
            uint addr = (uint)(registers.A & memory.ReadByte(registers.PC + 1));
            flags!.UpdateZSP(addr);
            flags!.UpdateCarryByte(addr);
            registers.A = (byte)(addr & 0xFF);
            registers.PC++;
            return 7;
        }

        private int OP_E7()
        {
            Call(0x20, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_E8()
        {
            if (flags!.P == 1)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_E9()
        {
            registers.PC = (ushort)(registers.H << 8 | registers.L);
            registers.PC--;
            return 5;
        }

        private int OP_EA()
        {
            if (flags!.P == 1)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_EB()
        {
            (registers.D, registers.H) = (registers.H, registers.D);
            (registers.L, registers.E) = (registers.E, registers.L);
            return 5;
        }

        private int OP_EC()
        {
            if (flags!.P == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(registers.PC + 3);
                Call(addr, retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_EE()
        {
            registers.A ^= memory.ReadByte(registers.PC + 1);
            flags!.UpdateCarryByte(registers.A);
            flags!.UpdateZSP(registers.A);
            registers.PC++;
            return 7;
        }

        private int OP_EF()
        {
            Call(0x28, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_F0()
        {
            if (flags!.P == 1)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_F1()
        {
            registers.A = memory.ReadByte(registers.SP + 1);
            var local_flags = memory.ReadByte(registers.SP);
            if (0x01 == (local_flags & 0x01)) flags!.Z = 0x01; else flags!.Z = 0x00;
            if (0x02 == (local_flags & 0x02)) flags!.S = 0x01; else flags!.S = 0x00;
            if (0x04 == (local_flags & 0x04)) flags!.P = 0x01; else flags!.P = 0x00;
            if (0x08 == (local_flags & 0x08)) flags!.CY = 0x01; else flags!.CY = 0x00;
            if (0x10 == (local_flags & 0x10)) flags!.AC = 0x01; else flags!.AC = 0x00;
            registers.SP += 2;
            return 10;
        }

        private int OP_F2()
        {
            if (flags!.P == 1)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_F3()
        {
            registers.INT_ENABLE = false;
            return 4;
        }

        private int OP_F4()
        {
            if (flags!.S == 0)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(registers.PC + 3);
                Call(addr, retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_F5()
        {
            memory.WriteByte(registers.SP - 1, registers.A);
            byte addr = ((byte)(flags!.Z | flags!.S << 1 | flags!.P << 2 | flags!.CY << 3 | flags!.AC << 4));
            memory.WriteByte(registers.SP - 2, addr);
            registers.SP -= 2;
            return 11;
        }

        private int OP_F6()
        {
            uint data = memory.ReadByte(registers.PC + 1);
            uint value = registers.A | data;
            flags!.UpdateCarryByte(value);
            flags!.UpdateZSP(value);
            registers.A = (byte)value;
            registers.PC++;
            return 7;
        }

        private int OP_F7()
        {
            Call(0x30, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }

        private int OP_F8()
        {
            if (flags!.S == 1)
            {
                registers.PC = (ushort)(memory.ReadByte(registers.SP + 1) << 8 | memory.ReadByte(registers.SP));
                registers.SP += 2;
                registers.PC--;
                return 11;
            }
            return 5;
        }

        private int OP_F9()
        {
            registers.SP = (ushort)registers.HL;
            return 5;
        }

        private int OP_FA()
        {
            if (flags!.S == 1)
            {
                var addr = ReadOpcodeDataWord();
                registers.PC = addr;
                registers.PC--;
            }
            else
            {
                registers.PC += 2;
            }
            return 10;
        }

        private int OP_FB()
        {
            registers.INT_ENABLE = true;
            return 4;
        }

        private int OP_FC()
        {
            if (flags!.S == 1)
            {
                var addr = ReadOpcodeDataWord();
                var retAddr = (ushort)(registers.PC + 3);
                Call(addr, retAddr);
                registers.PC--;
                return 17;
            }
            else
            {
                registers.PC += 2;
            }
            return 11;
        }

        private int OP_FE()
        {
            UInt16 addr = (UInt16)(registers.A + (byte)(~(memory.ReadByte(registers.PC + 1)) & 0xFF) + 1);
            flags!.UpdateCarryByte(addr);
            if (flags!.CY == 0) flags!.CY = 1; else flags!.CY = 0;
            flags!.UpdateZSP(addr);
            flags!.UpdateAuxCarryFlag(registers.A, (byte)(~(memory.ReadByte(registers.PC + 1)) & 0xFF), 1);
            registers.PC++;
            return 7;
        }

        private int OP_FF()
        {
            Call(0x38, (ushort)(registers.PC + 2));
            registers.PC--;
            return 11;
        }
    }
}
