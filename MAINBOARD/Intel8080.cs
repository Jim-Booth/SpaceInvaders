// ============================================================================
// Project:     SpaceInvaders
// File:        Intel8080.cs
// Description: Intel 8080 CPU emulator core with full opcode implementation,
//              interrupt handling, and cycle-accurate timing
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// Notice:      Space Invaders is (c) 1978 Taito Corporation.
//              This emulator is for educational purposes only.
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

        public ReadOnlySpan<byte> VideoSpan => _memory.Data.AsSpan((int)_videoStartAddress, 0x1C00);

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

        // Executes one full frame by running two half-frame CPU cycles
        // separated by mid-screen and full-screen interrupts.
        public bool RunFrame()
        {
            if (_paused) 
                return false;            
            ExecuteCycles(HalfFrameCyclesMax);  // 1st half of frame
            Interrupt(1);                  // mid screen Interrupt
            ExecuteCycles(HalfFrameCyclesMax);  // 2nd half of frame
            Interrupt(2);                  // full screen interrupt
            return true;
        }

        // Fetches and executes opcodes until the specified cycle budget is exhausted.
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

        // Stops the CPU by clearing the running flag.
        public void Stop()
        {
            _running = false;
        }

        // Reads the two-byte operand following the current opcode (little-endian) and returns it as a 16-bit word.
        private ushort ReadOpcodeDataWord()
        {
            return (ushort)(_memory.ReadByte(_registers.PC + 2) << 8 | _memory.ReadByte(_registers.PC + 1));
        }

        // Pushes the return address onto the stack and sets the program counter to the target address.
        private void Call(ushort address, ushort retAddress)
        {
            _memory.WriteByte(_registers.SP - 1, (byte)((retAddress >> 8) & 0xFF));
            _memory.WriteByte(_registers.SP - 2, (byte)(retAddress & 0xFF));
            _registers.PC = address;
            _registers.SP -= 2;
        }

        // Handles a hardware interrupt by pushing the current PC onto the stack
        // and jumping to the appropriate interrupt vector address.
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

        // Dispatches the given opcode to the corresponding OP_ handler method
        // and returns the number of CPU cycles consumed.
        private int CallOpcode(byte opcode)
        {
            return opcode switch
            {
                0x00 => OP_00(), // NOP
                0x01 => OP_01(), // LXI B
                0x02 => OP_02(), // STAX B
                0x03 => OP_03(), // INX B
                0x04 => OP_04(), // INR B
                0x05 => OP_05(), // DCR B
                0x06 => OP_06(), // MVI B
                0x07 => OP_07(), // RLC
                0x09 => OP_09(), // DAD B
                0x0A => OP_0A(), // LDAX B
                0x0B => OP_0B(), // DCX B
                0x0C => OP_0C(), // INR C
                0x0D => OP_0D(), // DCR C
                0x0E => OP_0E(), // MVI C
                0x0F => OP_0F(), // RRC
                0x11 => OP_11(), // LXI D
                0x12 => OP_12(), // STAX D
                0x13 => OP_13(), // INX D
                0x14 => OP_14(), // INR D
                0x15 => OP_15(), // DCR D
                0x16 => OP_16(), // MVI D
                0x17 => OP_17(), // RAL
                0x19 => OP_19(), // DAD D
                0x1A => OP_1A(), // LDAX D
                0x1B => OP_1B(), // DCX D
                0x1C => OP_1C(), // INR E
                0x1D => OP_1D(), // DCR E
                0x1E => OP_1E(), // MVI E
                0x1F => OP_1F(), // RAR
                0x20 => OP_20(), // RIM
                0x21 => OP_21(), // LXI H
                0x22 => OP_22(), // SHLD
                0x23 => OP_23(), // INX H
                0x24 => OP_24(), // INR H
                0x25 => OP_25(), // DCR H
                0x26 => OP_26(), // MVI H
                0x27 => OP_27(), // DAA
                0x29 => OP_29(), // DAD H
                0x2A => OP_2A(), // LHLD
                0x2B => OP_2B(), // DCX H
                0x2C => OP_2C(), // INR L
                0x2D => OP_2D(), // DCR L
                0x2E => OP_2E(), // MVI L
                0x2F => OP_2F(), // CMA
                0x30 => OP_30(), // SIM
                0x31 => OP_31(), // LXI SP
                0x32 => OP_32(), // STA
                0x33 => OP_33(), // INX SP
                0x34 => OP_34(), // INR M
                0x35 => OP_35(), // DCR M
                0x36 => OP_36(), // MVI M
                0x37 => OP_37(), // STC
                0x39 => OP_39(), // DAD SP
                0x3A => OP_3A(), // LDA
                0x3B => OP_3B(), // DCX SP
                0x3C => OP_3C(), // INR A
                0x3D => OP_3D(), // DCR A
                0x3E => OP_3E(), // MVI A
                0x3F => OP_3F(), // CMC
                0x40 => OP_40(), // MOV B,B
                0x41 => OP_41(), // MOV B,C
                0x42 => OP_42(), // MOV B,D
                0x43 => OP_43(), // MOV B,E
                0x44 => OP_44(), // MOV B,H
                0x45 => OP_45(), // MOV B,L
                0x46 => OP_46(), // MOV B,M
                0x47 => OP_47(), // MOV B,A
                0x48 => OP_48(), // MOV C,B
                0x49 => OP_49(), // MOV C,C
                0x4A => OP_4A(), // MOV C,D
                0x4B => OP_4B(), // MOV C,E
                0x4C => OP_4C(), // MOV C,H
                0x4D => OP_4D(), // MOV C,L
                0x4E => OP_4E(), // MOV C,M
                0x4F => OP_4F(), // MOV C,A
                0x50 => OP_50(), // MOV D,B
                0x51 => OP_51(), // MOV D,C
                0x52 => OP_52(), // MOV D,D
                0x53 => OP_53(), // MOV D,E
                0x54 => OP_54(), // MOV D,H
                0x55 => OP_55(), // MOV D,L
                0x56 => OP_56(), // MOV D,M
                0x57 => OP_57(), // MOV D,A
                0x58 => OP_58(), // MOV E,B
                0x59 => OP_59(), // MOV E,C
                0x5A => OP_5A(), // MOV E,D
                0x5B => OP_5B(), // MOV E,E
                0x5C => OP_5C(), // MOV E,H
                0x5D => OP_5D(), // MOV E,L
                0x5E => OP_5E(), // MOV E,M
                0x5F => OP_5F(), // MOV E,A
                0x60 => OP_60(), // MOV H,B
                0x61 => OP_61(), // MOV H,C
                0x62 => OP_62(), // MOV H,D
                0x63 => OP_63(), // MOV H,E
                0x64 => OP_64(), // MOV H,H
                0x65 => OP_65(), // MOV H,L
                0x66 => OP_66(), // MOV H,M
                0x67 => OP_67(), // MOV H,A
                0x68 => OP_68(), // MOV L,B
                0x69 => OP_69(), // MOV L,C
                0x6A => OP_6A(), // MOV L,D
                0x6B => OP_6B(), // MOV L,E
                0x6C => OP_6C(), // MOV L,H
                0x6D => OP_6D(), // MOV L,L
                0x6E => OP_6E(), // MOV L,M
                0x6F => OP_6F(), // MOV L,A
                0x70 => OP_70(), // MOV M,B
                0x71 => OP_71(), // MOV M,C
                0x72 => OP_72(), // MOV M,D
                0x73 => OP_73(), // MOV M,E
                0x74 => OP_74(), // MOV M,H
                0x75 => OP_75(), // MOV M,L
                0x76 => OP_76(), // HLT
                0x77 => OP_77(), // MOV M,A
                0x78 => OP_78(), // MOV A,B
                0x79 => OP_79(), // MOV A,C
                0x7A => OP_7A(), // MOV A,D
                0x7B => OP_7B(), // MOV A,E
                0x7C => OP_7C(), // MOV A,H
                0x7D => OP_7D(), // MOV A,L
                0x7E => OP_7E(), // MOV A,M
                0x7F => OP_7F(), // MOV A,A
                0x80 => OP_80(), // ADD B
                0x81 => OP_81(), // ADD C
                0x82 => OP_82(), // ADD D
                0x83 => OP_83(), // ADD E
                0x84 => OP_84(), // ADD H
                0x85 => OP_85(), // ADD L
                0x86 => OP_86(), // ADD M
                0x87 => OP_87(), // ADD A
                0x88 => OP_88(), // ADC B
                0x89 => OP_89(), // ADC C
                0x8A => OP_8A(), // ADC D
                0x8B => OP_8B(), // ADC E
                0x8C => OP_8C(), // ADC H
                0x8D => OP_8D(), // ADC L
                0x8E => OP_8E(), // ADC M
                0x8F => OP_8F(), // ADC A
                0x90 => OP_90(), // SUB B
                0x91 => OP_91(), // SUB C
                0x92 => OP_92(), // SUB D
                0x93 => OP_93(), // SUB E
                0x94 => OP_94(), // SUB H
                0x95 => OP_95(), // SUB L
                0x96 => OP_96(), // SUB M
                0x97 => OP_97(), // SUB A
                0x98 => OP_98(), // SBB B
                0x99 => OP_99(), // SBB C
                0x9A => OP_9A(), // SBB D
                0x9B => OP_9B(), // SBB E
                0x9C => OP_9C(), // SBB H
                0x9D => OP_9D(), // SBB L
                0x9E => OP_9E(), // SBB M
                0x9F => OP_9F(), // SBB A
                0xA0 => OP_A0(), // ANA B
                0xA1 => OP_A1(), // ANA C
                0xA2 => OP_A2(), // ANA D
                0xA3 => OP_A3(), // ANA E
                0xA4 => OP_A4(), // ANA H
                0xA5 => OP_A5(), // ANA L
                0xA6 => OP_A6(), // ANA M
                0xA7 => OP_A7(), // ANA A
                0xA8 => OP_A8(), // XRA B
                0xA9 => OP_A9(), // XRA C
                0xAA => OP_AA(), // XRA D
                0xAB => OP_AB(), // XRA E
                0xAC => OP_AC(), // XRA H
                0xAD => OP_AD(), // XRA L
                0xAE => OP_AE(), // XRA M
                0xAF => OP_AF(), // XRA A
                0xB0 => OP_B0(), // ORA B
                0xB1 => OP_B1(), // ORA C
                0xB2 => OP_B2(), // ORA D
                0xB3 => OP_B3(), // ORA E
                0xB4 => OP_B4(), // ORA H
                0xB5 => OP_B5(), // ORA L
                0xB6 => OP_B6(), // ORA M
                0xB7 => OP_B7(), // ORA A
                0xB8 => OP_B8(), // CMP B
                0xB9 => OP_B9(), // CMP C
                0xBA => OP_BA(), // CMP D
                0xBB => OP_BB(), // CMP E
                0xBC => OP_BC(), // CMP H
                0xBD => OP_BD(), // CMP L
                0xBE => OP_BE(), // CMP M
                0xBF => OP_BF(), // CMP A
                0xC0 => OP_C0(), // RNZ
                0xC1 => OP_C1(), // POP B
                0xC2 => OP_C2(), // JNZ
                0xC3 => OP_C3(), // JMP
                0xC4 => OP_C4(), // CNZ
                0xC5 => OP_C5(), // PUSH B
                0xC6 => OP_C6(), // ADI
                0xC7 => OP_C7(), // RST 7
                0xC8 => OP_C8(), // RZ
                0xC9 => OP_C9(), // RET
                0xCA => OP_CA(), // JZ
                0xCC => OP_CC(), // CZ
                0xCD => OP_CD(), // CALL
                0xCE => OP_CE(), // ACI
                0xCF => OP_CF(), // RST 1
                0xD0 => OP_D0(), // RNC
                0xD1 => OP_D1(), // POP D
                0xD2 => OP_D2(), // JNC
                0xD3 => OP_D3(), // OUT
                0xD4 => OP_D4(), // CNC
                0xD5 => OP_D5(), // PUSH D
                0xD6 => OP_D6(), // SUI
                0xD7 => OP_D7(), // RST 2
                0xD8 => OP_D8(), // RC
                0xDA => OP_DA(), // JC
                0xDB => OP_DB(), // IN
                0xDC => OP_DC(), // CC
                0xDE => OP_DE(), // SBI
                0xDF => OP_DF(), // RST 3
                0xE0 => OP_E0(), // RPO
                0xE1 => OP_E1(), // POP H
                0xE2 => OP_E2(), // JPO
                0xE3 => OP_E3(), // XTHL
                0xE4 => OP_E4(), // CPO
                0xE5 => OP_E5(), // PUSH H
                0xE6 => OP_E6(), // ANI
                0xE7 => OP_E7(), // RST 4
                0xE8 => OP_E8(), // RPE
                0xE9 => OP_E9(), // PCHL
                0xEA => OP_EA(), // JPE
                0xEB => OP_EB(), // XCHG
                0xEC => OP_EC(), // CPE
                0xEE => OP_EE(), // XRI
                0xEF => OP_EF(), // RST 5
                0xF0 => OP_F0(), // RP
                0xF1 => OP_F1(), // POP PSW
                0xF2 => OP_F2(), // JP
                0xF3 => OP_F3(), // DI
                0xF4 => OP_F4(), // CP
                0xF5 => OP_F5(), // PUSH PSW
                0xF6 => OP_F6(), // ORI
                0xF7 => OP_F7(), // RST 6
                0xF8 => OP_F8(), // RM
                0xF9 => OP_F9(), // SPHL
                0xFA => OP_FA(), // JM
                0xFB => OP_FB(), // EI
                0xFC => OP_FC(), // CM
                0xFE => OP_FE(), // CPI
                0xFF => OP_FF(), // RST 7
                _ => throw new NotImplementedException("INVALID OPCODE - " + opcode.ToString("X2")),
            };
        }

        // NOP — No operation
        private static int OP_00()
        {
            return 4;
        }

        // LXI B — Load immediate word into BC
        private int OP_01()
        {
            _registers.C = _memory.ReadByte(_registers.PC + 1);
            _registers.B = _memory.ReadByte(_registers.PC + 2);
            _registers.PC += 2;
            return 10;
        }

        // STAX B — Store A to address in BC
        private int OP_02()
        {
            _memory.WriteByte(_registers.BC, _registers.A);
            return 7;
        }

        // INX B — Increment register pair BC
        private int OP_03()
        {
            uint addr = _registers.BC;
            addr++;
            _registers.BC = addr;
            return 5;
        }

        // INR B — Increment register B
        private int OP_04()
        {
            _registers.B++;
            _flags!.UpdateZSP(_registers.B);
            return 5;
        }

        // DCR B — Decrement register B
        private int OP_05()
        {
            _registers.B--;
            _flags!.UpdateZSP(_registers.B);
            return 5;
        }

        // MVI B — Move immediate byte into B
        private int OP_06()
        {
            _registers.B = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        // RLC — Rotate A left, bit 7 to carry and bit 0
        private int OP_07()
        {
            int bit7 = ((_registers.A & 0x80) == 0x80) ? 1 : 0;
            _registers.A = (byte)((_registers.A << 1) | bit7);
            _flags!.CY = (byte)bit7;
            return 4;
        }

        // DAD B — Add BC to HL
        private int OP_09()
        {
            uint addr = _registers.HL + _registers.BC;
            _flags!.UpdateCarryWord(addr);
            _registers.HL = addr & 0xFFFF;
            return 10;
        }

        // LDAX B — Load A from address in BC
        private int OP_0A()
        {
            uint addr = _registers.BC;
            _registers.A = _memory.ReadByte(addr);
            return 7;
        }

        // DCX B — Decrement register pair BC
        private int OP_0B()
        {
            uint addr = _registers.BC;
            addr--;
            _registers.BC = addr;
            return 5;
        }

        // INR C — Increment register C
        private int OP_0C()
        {
            _registers.C++;
            _flags!.UpdateZSP(_registers.C);
            return 5;
        }

        // DCR C — Decrement register C
        private int OP_0D()
        {
            _registers.C--;
            _flags!.UpdateZSP(_registers.C);
            return 5;
        }

        // MVI C — Move immediate byte into C
        private int OP_0E()
        {
            _registers.C = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        // RRC — Rotate A right, bit 0 to carry and bit 7
        private int OP_0F()
        {
            int bit0 = _registers.A & 0x01;
            _registers.A >>= 1;
            _registers.A |= (byte)(bit0 << 7);
            _flags!.CY = (byte)bit0;
            return 4;
        }

        // LXI D — Load immediate word into DE
        private int OP_11()
        {
            _registers.D = _memory.ReadByte(_registers.PC + 2);
            _registers.E = _memory.ReadByte(_registers.PC + 1);
            _registers.PC += 2;
            return 10;
        }

        // STAX D — Store A to address in DE
        private int OP_12()
        {
            uint addr = _registers.DE;
            _memory.WriteByte(addr, _registers.A);
            return 7;
        }

        // INX D — Increment register pair DE
        private int OP_13()
        {
            uint addr = _registers.DE; ;
            addr++;
            _registers.DE = addr;
            return 5;
        }

        // INR D — Increment register D
        private int OP_14()
        {
            _registers.D++;
            _flags!.UpdateZSP(_registers.D);
            return 5;
        }

        // DCR D — Decrement register D
        private int OP_15()
        {
            _registers.D--;
            _flags!.UpdateZSP(_registers.D);
            return 5;
        }

        // MVI D — Move immediate byte into D
        private int OP_16()
        {
            _registers.D = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        // RAL — Rotate A left through carry
        private int OP_17()
        {
            uint bit7 = (uint)(((_registers.A & 128) == 128) ? 1 : 0);
            uint bit0 = _flags!.CY;
            _registers.A = (byte)((uint)(_registers.A << 1) | bit0);
            _flags!.CY = bit7;
            return 4;
        }

        // DAD D — Add DE to HL
        private int OP_19()
        {
            uint addr = _registers.DE + _registers.HL;
            _flags!.UpdateCarryWord(addr);
            _registers.HL = addr & 0xFFFF;
            return 10;
        }

        // LDAX D — Load A from address in DE
        private int OP_1A()
        {
            uint addr = _registers.DE;
            _registers.A = _memory.ReadByte(addr);
            return 7;
        }

        // DCX D — Decrement register pair DE
        private int OP_1B()
        {
            var addr = (ushort)_registers.DE;
            addr--;
            _registers.DE = addr;
            return 5;
        }

        // INR E — Increment register E
        private int OP_1C()
        {
            _registers.E++;
            _flags!.UpdateZSP(_registers.E);
            return 5;
        }

        // DCR E — Decrement register E
        private int OP_1D()
        {
            _registers.E--;
            _flags!.UpdateZSP(_registers.E);
            return 5;
        }

        // MVI E — Move immediate byte into E
        private int OP_1E()
        {
            _registers.E = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        // RAR — Rotate A right through carry
        private int OP_1F()
        {
            int bit0 = _registers.A & 1;
            uint bit7 = _flags!.CY;
            _registers.A = (byte)((uint)(_registers.A >> 1) | (bit7 << 7));
            _flags!.CY = (byte)bit0;
            return 4;
        }

        // RIM — Read interrupt mask (8085 only, treated as NOP)
        private static int OP_20()
        { return 4; }

        // LXI H — Load immediate word into HL
        private int OP_21()
        {
            _registers.H = _memory.ReadByte(_registers.PC + 2);
            _registers.L = _memory.ReadByte(_registers.PC + 1);
            _registers.PC += 2;
            return 10;
        }

        // SHLD — Store HL to memory at direct address
        private int OP_22()
        {
            ushort addr = ReadOpcodeDataWord();
            _memory.WriteByte(addr, _registers.L);
            _memory.WriteByte((uint)addr + 1, _registers.H);
            _registers.PC += 2;
            return 16;
        }

        // INX H — Increment register pair HL
        private int OP_23()
        {
            uint addr = _registers.HL;
            addr++;
            _registers.HL = addr;
            return 5;
        }

        // INR H — Increment register H
        private int OP_24()
        {
            _registers.H++;
            _flags!.UpdateZSP(_registers.H);
            return 5;
        }

        // DCR H — Decrement register H
        private int OP_25()
        {
            _registers.H--;
            _flags!.UpdateZSP(_registers.H);
            return 5;
        }

        // MVI H — Move immediate byte into H
        private int OP_26()
        {
            _registers.H = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        // DAA — Decimal adjust accumulator for BCD arithmetic
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

        // DAD H — Add HL to HL (double HL)
        private int OP_29()
        {
            uint addr = _registers.HL + _registers.HL;
            _flags!.UpdateCarryWord(addr);
            _registers.HL = addr & 0xFFFF;
            return 10;
        }

        // LHLD — Load HL from memory at direct address
        private int OP_2A()
        {
            ushort addr = ReadOpcodeDataWord();
            _registers.L = _memory.ReadByte(addr);
            _registers.H = _memory.ReadByte((uint)addr + 1);
            _registers.PC += 2;
            return 16;
        }

        // DCX H — Decrement register pair HL
        private int OP_2B()
        {
            uint addr = _registers.HL;
            addr--;
            _registers.HL = addr;
            return 5;
        }

        // INR L — Increment register L
        private int OP_2C()
        {
            _registers.L++;
            _flags!.UpdateZSP(_registers.L);
            return 5;
        }

        // DCR L — Decrement register L
        private int OP_2D()
        {
            _registers.L--;
            _flags!.UpdateZSP(_registers.L);
            return 5;
        }

        // MVI L — Move immediate byte into L
        private int OP_2E()
        {
            _registers.L = _memory.ReadByte(_registers.PC + 1);
            _registers.PC++;
            return 7;
        }

        // CMA — Complement accumulator (bitwise NOT)
        private int OP_2F()
        {
            _registers.A = (byte)~_registers.A;
            return 7;
        }

        // SIM — Set interrupt mask (8085 only, treated as NOP)
        private static int OP_30()
        { return 4; }

        // LXI SP — Load immediate word into SP
        private int OP_31()
        {
            _registers.SP = ReadOpcodeDataWord();
            _registers.PC += 2;
            return 10;
        }

        // STA — Store A to direct address
        private int OP_32()
        {
            ushort addr = ReadOpcodeDataWord();
            _memory.WriteByte(addr, _registers.A);
            _registers.PC += 2;
            return 15;
        }

        // INX SP — Increment stack pointer
        private int OP_33()
        {
            _registers.SP++;
            return 5;
        }

        // INR M — Increment memory byte at address in HL
        private int OP_34()
        {
            uint addr = _registers.HL;
            byte value = _memory.ReadByte(addr);
            value++;
            _flags!.UpdateZSP(value);
            _memory.WriteByte(addr, (byte)(value & 0xFF));
            return 10;
        }

        // DCR M — Decrement memory byte at address in HL
        private int OP_35()
        {
            uint addr = _registers.HL;
            byte value = _memory.ReadByte(addr);
            value--;
            _flags!.UpdateZSP(value);
            _memory.WriteByte(addr, (byte)(value & 0xFF));
            return 10;
        }

        // MVI M — Move immediate byte to memory at HL
        private int OP_36()
        {
            uint addr = _registers.HL;
            byte value = _memory.ReadByte(_registers.PC + 1);
            _memory.WriteByte(addr, value);
            _registers.PC++;
            return 10;
        }

        // STC — Set carry flag
        private int OP_37()
        {
            _flags!.CY = 1;
            return 4;
        }

        // DAD SP — Add SP to HL
        private int OP_39()
        {
            uint value = _registers.HL + _registers.SP;
            _flags!.UpdateCarryWord(value);
            _registers.HL = (value & 0xFFFF);
            return 10;
        }

        // LDA — Load A from direct address
        private int OP_3A()
        {
            ushort addr = ReadOpcodeDataWord();
            _registers.A = _memory.ReadByte(addr);
            _registers.PC += 2;
            return 13;
        }

        // DCX SP — Decrement stack pointer
        private int OP_3B()
        {
            _registers.SP--;
            return 5;
        }

        // INR A — Increment accumulator
        private int OP_3C()
        {
            _registers.A++;
            _flags!.UpdateZSP(_registers.A);
            return 5;
        }

        // DCR A — Decrement accumulator
        private int OP_3D()
        {
            _registers.A--;
            _flags!.UpdateZSP(_registers.A);
            return 5;
        }

        // MVI A — Move immediate byte into A
        private int OP_3E()
        {
            byte addr = _memory.ReadByte(_registers.PC + 1);
            _registers.A = addr;
            _registers.PC++;
            return 7;
        }

        // CMC — Complement carry flag
        private int OP_3F()
        {
            _flags!.CY = (byte)~_flags!.CY;
            return 4;
        }

        // MOV B,B — Move B into B (no-op)
        private int OP_40()
        {
            _registers.B = _registers.B;
            return 5;
        }

        // MOV B,C — Move C into B
        private int OP_41()
        {
            _registers.B = _registers.C;
            return 5;
        }

        // MOV B,D — Move D into B
        private int OP_42()
        {
            _registers.B = _registers.D;
            return 5;
        }

        // MOV B,E — Move E into B
        private int OP_43()
        {
            _registers.B = _registers.E;
            return 5;
        }

        // MOV B,H — Move H into B
        private int OP_44()
        {
            _registers.B = _registers.H;
            return 5;
        }

        // MOV B,L — Move L into B
        private int OP_45()
        {
            _registers.B = _registers.L;
            return 5;
        }

        // MOV B,M — Move memory byte at HL into B
        private int OP_46()
        {
            uint addr = _registers.HL;
            _registers.B = _memory.ReadByte(addr);
            return 7;
        }

        // MOV B,A — Move A into B
        private int OP_47()
        {
            _registers.B = _registers.A;
            return 5;
        }

        // MOV C,B — Move B into C
        private int OP_48()
        {
            _registers.C = _registers.B;
            return 5;
        }

        // MOV C,C — Move C into C (no-op)
        private int OP_49()
        {
            _registers.C = _registers.C;
            return 5;
        }

        // MOV C,D — Move D into C
        private int OP_4A()
        {
            _registers.C = _registers.D;
            return 5;
        }

        // MOV C,E — Move E into C
        private int OP_4B()
        {
            _registers.C = _registers.E;
            return 5;
        }

        // MOV C,H — Move H into C
        private int OP_4C()
        {
            _registers.C = _registers.H;
            return 5;
        }

        // MOV C,L — Move L into C
        private int OP_4D()
        {
            _registers.C = _registers.L;
            return 5;
        }

        // MOV C,M — Move memory byte at HL into C
        private int OP_4E()
        {
            uint addr = _registers.HL;
            _registers.C = _memory.ReadByte(addr);
            return 7;
        }

        // MOV C,A — Move A into C
        private int OP_4F()
        {
            _registers.C = _registers.A;
            return 5;
        }

        // MOV D,B — Move B into D
        private int OP_50()
        {
            _registers.D = _registers.B;
            return 5;
        }

        // MOV D,C — Move C into D
        private int OP_51()
        {
            _registers.D = _registers.C;
            return 5;
        }

        // MOV D,D — Move D into D (no-op)
        private int OP_52()
        {
            _registers.D = _registers.D;
            return 5;
        }

        // MOV D,E — Move E into D
        private int OP_53()
        {
            _registers.D = _registers.E;
            return 5;
        }

        // MOV D,H — Move H into D
        private int OP_54()
        {
            _registers.D = _registers.H;
            return 5;
        }

        // MOV D,L — Move L into D
        private int OP_55()
        {
            _registers.D = _registers.L;
            return 5;
        }

        // MOV D,M — Move memory byte at HL into D
        private int OP_56()
        {
            uint addr = _registers.HL;
            _registers.D = _memory.ReadByte(addr);
            return 7;
        }

        // MOV D,A — Move A into D
        private int OP_57()
        {
            _registers.D = _registers.A;
            return 5;
        }

        // MOV E,B — Move B into E
        private int OP_58()
        {
            _registers.E = _registers.B;
            return 5;
        }

        // MOV E,C — Move C into E
        private int OP_59()
        {
            _registers.E = _registers.C;
            return 5;
        }

        // MOV E,D — Move D into E
        private int OP_5A()
        {
            _registers.E = _registers.D;
            return 5;
        }

        // MOV E,E — Move E into E (no-op)
        private int OP_5B()
        {
            _registers.E = _registers.E;
            return 5;
        }

        // MOV E,H — Move H into E
        private int OP_5C()
        {
            _registers.E = _registers.H;
            return 5;
        }

        // MOV E,L — Move L into E
        private int OP_5D()
        {
            _registers.E = _registers.L;
            return 5;
        }

        // MOV E,M — Move memory byte at HL into E
        private int OP_5E()
        {
            uint addr = _registers.HL;
            _registers.E = _memory.ReadByte(addr);
            return 7;
        }

        // MOV E,A — Move A into E
        private int OP_5F()
        {
            _registers.E = _registers.A;
            return 5;
        }

        // MOV H,B — Move B into H
        private int OP_60()
        {
            _registers.H = _registers.B;
            return 5;
        }

        // MOV H,C — Move C into H
        private int OP_61()
        {
            _registers.H = _registers.C;
            return 5;
        }

        // MOV H,D — Move D into H
        private int OP_62()
        {
            _registers.H = _registers.D;
            return 5;
        }

        // MOV H,E — Move E into H
        private int OP_63()
        {
            _registers.H = _registers.E;
            return 5;
        }

        // MOV H,H — Move H into H (no-op)
        private int OP_64()
        {
            _registers.H = _registers.H;
            return 5;
        }

        // MOV H,L — Move L into H
        private int OP_65()
        {
            _registers.H = _registers.L;
            return 5;
        }

        // MOV H,M — Move memory byte at HL into H
        private int OP_66()
        {
            uint addr = _registers.HL;
            _registers.H = _memory.ReadByte(addr);
            return 7;
        }

        // MOV H,A — Move A into H
        private int OP_67()
        {
            _registers.H = _registers.A;
            return 5;
        }

        // MOV L,B — Move B into L
        private int OP_68()
        {
            _registers.L = _registers.B;
            return 5;
        }

        // MOV L,C — Move C into L
        private int OP_69()
        {
            _registers.L = _registers.C;
            return 5;
        }

        // MOV L,D — Move D into L
        private int OP_6A()
        {
            _registers.L = _registers.D;
            return 5;
        }

        // MOV L,E — Move E into L
        private int OP_6B()
        {
            _registers.L = _registers.E;
            return 5;
        }

        // MOV L,H — Move H into L
        private int OP_6C()
        {
            _registers.L = _registers.H;
            return 5;
        }

        // MOV L,L — Move L into L (no-op)
        private int OP_6D()
        {
            _registers.L = _registers.L;
            return 5;
        }

        // MOV L,M — Move memory byte at HL into L
        private int OP_6E()
        {
            uint addr = _registers.HL;
            _registers.L = _memory.ReadByte(addr);
            return 7;
        }

        // MOV L,A — Move A into L
        private int OP_6F()
        {
            _registers.L = _registers.A;
            return 5;
        }

        // MOV M,B — Move B to memory at HL
        private int OP_70()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.B);
            return 7;
        }

        // MOV M,C — Move C to memory at HL
        private int OP_71()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.C);
            return 7;
        }

        // MOV M,D — Move D to memory at HL
        private int OP_72()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.D);
            return 7;
        }

        // MOV M,E — Move E to memory at HL
        private int OP_73()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.E);
            return 7;
        }

        // MOV M,H — Move H to memory at HL
        private int OP_74()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.H);
            return 7;
        }

        // MOV M,L — Move L to memory at HL
        private int OP_75()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.L);
            return 7;
        }

        // HLT — Halt processor
        private int OP_76()
        {
            _running = false;
            return 7;
        }

        // MOV M,A — Move A to memory at HL
        private int OP_77()
        {
            uint addr = _registers.HL;
            _memory.WriteByte(addr, _registers.A);
            return 7;
        }

        // MOV A,B — Move B into A
        private int OP_78()
        {
            _registers.A = _registers.B;
            return 5;
        }

        // MOV A,C — Move C into A
        private int OP_79()
        {
            _registers.A = _registers.C;
            return 5;
        }

        // MOV A,D — Move D into A
        private int OP_7A()
        {
            _registers.A = _registers.D;
            return 5;
        }

        // MOV A,E — Move E into A
        private int OP_7B()
        {
            _registers.A = _registers.E;
            return 5;
        }

        // MOV A,H — Move H into A
        private int OP_7C()
        {
            _registers.A = _registers.H;
            return 5;
        }

        // MOV A,L — Move L into A
        private int OP_7D()
        {
            _registers.A = _registers.L;
            return 5;
        }

        // MOV A,M — Move memory byte at HL into A
        private int OP_7E()
        {
            uint addr = _registers.HL;
            _registers.A = _memory.ReadByte(addr);
            return 7;
        }

        // MOV A,A — Move A into A (no-op)
        private int OP_7F()
        {
            _registers.A = _registers.A;
            return 5;
        }

        // ADD B — Add B to A
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

        // ADD C — Add C to A
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

        // ADD D — Add D to A
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

        // ADD E — Add E to A
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

        // ADD H — Add H to A
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

        // ADD L — Add L to A
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

        // ADD M — Add memory byte at HL to A
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

        // ADD A — Add A to A (double A)
        private int OP_87()
        {
            var addr = (uint)_registers.A + (uint)_registers.A;
            _flags!.UpdateAuxCarryFlag(_registers.A, _registers.A);
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            return 4;
        }

        // ADC B — Add B to A with carry
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

        // ADC C — Add C to A with carry
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

        // ADC D — Add D to A with carry
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

        // ADC E — Add E to A with carry
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

        // ADC H — Add H to A with carry
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

        // ADC L — Add L to A with carry
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

        // ADC M — Add memory byte at HL to A with carry
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

        // ADC A — Add A to A with carry
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

        // SUB B — Subtract B from A
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

        // SUB C — Subtract C from A
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

        // SUB D — Subtract D from A
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

        // SUB E — Subtract E from A
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

        // SUB H — Subtract H from A
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

        // SUB L — Subtract L from A
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

        // SUB M — Subtract memory byte at HL from A
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

        // SUB A — Subtract A from A (zero A)
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

        // SBB B — Subtract B from A with borrow
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

        // SBB C — Subtract C from A with borrow
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

        // SBB D — Subtract D from A with borrow
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

        // SBB E — Subtract E from A with borrow
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

        // SBB H — Subtract H from A with borrow
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

        // SBB L — Subtract L from A with borrow
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

        // SBB M — Subtract memory byte at HL from A with borrow
        private int OP_9E()
        {
            uint reg = _memory.ReadByte(_registers.HL);
            if (_flags!.CY == 1)
                reg += 1;
            var addr = (uint)(_registers.A + (~reg & 0xff) + 1);
            _flags!.UpdateCarryByte(addr);
            if (_flags!.CY == 0) _flags!.CY = 1; else _flags!.CY = 0;
            _flags!.UpdateZSP((byte)addr);
            _flags!.UpdateAuxCarryFlag(_registers.A, (byte)((~reg & 0xff) & 0xFF));
            _registers.A = (byte)(addr & 0xFF);
            return 7;
        }

        // SBB A — Subtract A from A with borrow
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

        // ANA B — AND B with A
        private int OP_A0()
        {
            _registers.A = (byte)(_registers.A & _registers.B);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ANA C — AND C with A
        private int OP_A1()
        {
            _registers.A = (byte)(_registers.A & _registers.C);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ANA D — AND D with A
        private int OP_A2()
        {
            _registers.A = (byte)(_registers.A & _registers.D);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ANA E — AND E with A
        private int OP_A3()
        {
            _registers.A = (byte)(_registers.A & _registers.E);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ANA H — AND H with A
        private int OP_A4()
        {
            _registers.A = (byte)(_registers.A & _registers.H);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ANA L — AND L with A
        private int OP_A5()
        {
            _registers.A = (byte)(_registers.A & _registers.L);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ANA M — AND memory byte at HL with A
        private int OP_A6()
        {
            _registers.A = (byte)(_registers.A & _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 7;
        }

        // ANA A — AND A with A (clears CY/AC)
        private int OP_A7()
        {
            _registers.A = (byte)(_registers.A & _registers.A);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA B — XOR B with A
        private int OP_A8()
        {
            _registers.A = (byte)(_registers.A ^ _registers.B);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA C — XOR C with A
        private int OP_A9()
        {
            _registers.A = (byte)(_registers.A ^ _registers.C);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA D — XOR D with A
        private int OP_AA()
        {
            _registers.A = (byte)(_registers.A ^ _registers.D);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA E — XOR E with A
        private int OP_AB()
        {
            _registers.A = (byte)(_registers.A ^ _registers.E);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA H — XOR H with A
        private int OP_AC()
        {
            _registers.A = (byte)(_registers.A ^ _registers.H);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA L — XOR L with A
        private int OP_AD()
        {
            _registers.A = (byte)(_registers.A ^ _registers.L);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // XRA M — XOR memory byte at HL with A
        private int OP_AE()
        {
            _registers.A = (byte)(_registers.A ^ _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 7;
        }

        // XRA A — XOR A with A (zero A)
        private int OP_AF()
        {
            _registers.A = (byte)(_registers.A ^ _registers.A);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA B — OR B with A
        private int OP_B0()
        {
            _registers.A = (byte)(_registers.A | _registers.B);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA C — OR C with A
        private int OP_B1()
        {
            _registers.A = (byte)(_registers.A | _registers.C);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA D — OR D with A
        private int OP_B2()
        {
            _registers.A = (byte)(_registers.A | _registers.D);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA E — OR E with A
        private int OP_B3()
        {
            _registers.A = (byte)(_registers.A | _registers.E);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA H — OR H with A
        private int OP_B4()
        {
            _registers.A = (byte)(_registers.A | _registers.H);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA L — OR L with A
        private int OP_B5()
        {
            _registers.A = (byte)(_registers.A | _registers.L);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // ORA M — OR memory byte at HL with A
        private int OP_B6()
        {
            _registers.A = (byte)(_registers.A | _memory.ReadByte(_registers.HL));
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 7;
        }

        // ORA A — OR A with A (clears CY/AC)
        private int OP_B7()
        {
            _registers.A = (byte)(_registers.A | _registers.A);
            _flags!.UpdateZSP(_registers.A);
            _flags!.CY = 0;
            _flags!.AC = 0;
            return 4;
        }

        // CMP B — Compare B with A
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

        // CMP C — Compare C with A
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

        // CMP D — Compare D with A
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

        // CMP E — Compare E with A
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

        // CMP H — Compare H with A
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

        // CMP L — Compare L with A
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

        // CMP M — Compare memory byte at HL with A
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

        // CMP A — Compare A with A (always sets Z)
        private int OP_BF()
        {
            _flags!.Z = 1;
            _flags!.S = 0;
            _flags!.P = Flags.CalculateParityFlag(_registers.A);
            _flags!.CY = 0;
            return 4;
        }

        // RNZ — Return if not zero
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

        // POP B — Pop BC from stack
        private int OP_C1()
        {
            _registers.C = _memory.ReadByte(_registers.SP);
            _registers.B = _memory.ReadByte(_registers.SP + 1);
            _registers.SP += 2;
            return 10;
        }

        // JNZ — Jump if not zero
        private int OP_C2()
        {
            if (_flags!.Z == 0)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // JMP — Unconditional jump
        private int OP_C3()
        {
            ushort addr = ReadOpcodeDataWord();
            _registers.PC = addr;
            _registers.PC--;
            return 10;
        }

        // CNZ — Call if not zero
        private int OP_C4()
        {
            if (_flags!.Z == 0)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // PUSH B — Push BC onto stack
        private int OP_C5()
        {
            _memory.WriteByte(_registers.SP - 2, _registers.C);
            _memory.WriteByte(_registers.SP - 1, _registers.B);
            _registers.SP -= 2;
            return 11;
        }

        // ADI — Add immediate byte to A
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

        // RST 7 — Restart (call 0x0038)
        private int OP_C7()
        {
            Call(0x38, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RZ — Return if zero
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

        // RET — Unconditional return
        private int OP_C9()
        {
            _registers.PC = (ushort)(_memory.ReadByte(_registers.SP + 1) << 8 | _memory.ReadByte(_registers.SP));
            _registers.SP += 2;
            _registers.PC--;
            return 10;
        }

        // JZ — Jump if zero
        private int OP_CA()
        {
            if (_flags!.Z == 1)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // CZ — Call if zero
        private int OP_CC()
        {
            if (_flags!.Z == 1)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // CALL — Unconditional call
        private int OP_CD()
        {
            ushort addr = ReadOpcodeDataWord();
            var retAddr = (ulong)(_registers.PC + 3);
            Call((ushort)addr, (ushort)retAddr);
            _registers.PC--;
            return 17;
        }

        // ACI — Add immediate byte to A with carry
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

        // RST 1 — Restart (call 0x0008)
        private int OP_CF()
        {
            Call(0x08, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RNC — Return if no carry
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

        // POP D — Pop DE from stack
        private int OP_D1()
        {
            _registers.E = _memory.ReadByte(_registers.SP);
            _registers.D = _memory.ReadByte(_registers.SP + 1);
            _registers.SP += 2;
            return 10;
        }

        // JNC — Jump if no carry
        private int OP_D2()
        {
            if (_flags!.CY == 0)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = (ushort)addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // OUT — Write A to output port
        private int OP_D3()
        {
            byte port = _memory.ReadByte(_registers.PC + 1);
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
            return 10;
        }

        // CNC — Call if no carry
        private int OP_D4()
        {
            if (_flags!.CY == 0)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // PUSH D — Push DE onto stack
        private int OP_D5()
        {
            _memory.WriteByte(_registers.SP - 2, _registers.E);
            _memory.WriteByte(_registers.SP - 1, _registers.D);
            _registers.SP -= 2;
            return 11;
        }

        // SUI — Subtract immediate byte from A
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

        // RST 2 — Restart (call 0x0010)
        private int OP_D7()
        {
            Call(0x10, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RC — Return if carry
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

        // JC — Jump if carry
        private int OP_DA()
        {
            if (_flags!.CY == 1)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // IN — Read input port into A
        private int OP_DB()
        {
            byte port = _memory.ReadByte(_registers.PC + 1);
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

        // CC — Call if carry
        private int OP_DC()
        {
            if (_flags!.CY == 1)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // SBI — Subtract immediate byte from A with borrow
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

        // RST 3 — Restart (call 0x0018)
        private int OP_DF()
        {
            Call(0x18, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RPO — Return if parity odd
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

        // POP H — Pop HL from stack
        private int OP_E1()
        {
            _registers.L = _memory.ReadByte(_registers.SP);
            _registers.H = _memory.ReadByte(_registers.SP + 1);
            _registers.SP += 2;
            return 10;
        }

        // JPO — Jump if parity odd
        private int OP_E2()
        {
            if (_flags!.P == 0)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // XTHL — Exchange HL with top of stack
        private int OP_E3()
        {
            byte l = _registers.L;
            byte h = _registers.H;
            _registers.L = _memory.ReadByte(_registers.SP);
            _registers.H = _memory.ReadByte(_registers.SP + 1);
            _memory.WriteByte(_registers.SP, l);
            _memory.WriteByte(_registers.SP + 1, h);
            return 18;
        }

        // CPO — Call if parity odd
        private int OP_E4()
        {
            if (_flags!.P == 0)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // PUSH H — Push HL onto stack
        private int OP_E5()
        {
            _memory.WriteByte(_registers.SP - 2, _registers.L);
            _memory.WriteByte(_registers.SP - 1, _registers.H);
            _registers.SP -= 2;
            return 11;
        }

        // ANI — AND immediate byte with A
        private int OP_E6()
        {
            uint addr = (uint)(_registers.A & _memory.ReadByte(_registers.PC + 1));
            _flags!.UpdateZSP(addr);
            _flags!.UpdateCarryByte(addr);
            _registers.A = (byte)(addr & 0xFF);
            _registers.PC++;
            return 7;
        }

        // RST 4 — Restart (call 0x0020)
        private int OP_E7()
        {
            Call(0x20, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RPE — Return if parity even
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

        // PCHL — Jump to address in HL
        private int OP_E9()
        {
            _registers.PC = (ushort)(_registers.H << 8 | _registers.L);
            _registers.PC--;
            return 5;
        }

        // JPE — Jump if parity even
        private int OP_EA()
        {
            if (_flags!.P == 1)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // XCHG — Exchange DE and HL
        private int OP_EB()
        {
            (_registers.D, _registers.H) = (_registers.H, _registers.D);
            (_registers.L, _registers.E) = (_registers.E, _registers.L);
            return 5;
        }

        // CPE — Call if parity even
        private int OP_EC()
        {
            if (_flags!.P == 1)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // XRI — XOR immediate byte with A
        private int OP_EE()
        {
            _registers.A ^= _memory.ReadByte(_registers.PC + 1);
            _flags!.UpdateCarryByte(_registers.A);
            _flags!.UpdateZSP(_registers.A);
            _registers.PC++;
            return 7;
        }

        // RST 5 — Restart (call 0x0028)
        private int OP_EF()
        {
            Call(0x28, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RP — Return if positive (sign flag clear)
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

        // POP PSW — Pop A and flags from stack
        private int OP_F1()
        {
            _registers.A = _memory.ReadByte(_registers.SP + 1);
            byte local_flags = _memory.ReadByte(_registers.SP);
            if (0x01 == (local_flags & 0x01)) _flags!.Z = 0x01; else _flags!.Z = 0x00;
            if (0x02 == (local_flags & 0x02)) _flags!.S = 0x01; else _flags!.S = 0x00;
            if (0x04 == (local_flags & 0x04)) _flags!.P = 0x01; else _flags!.P = 0x00;
            if (0x08 == (local_flags & 0x08)) _flags!.CY = 0x01; else _flags!.CY = 0x00;
            if (0x10 == (local_flags & 0x10)) _flags!.AC = 0x01; else _flags!.AC = 0x00;
            _registers.SP += 2;
            return 10;
        }

        // JP — Jump if positive (sign flag clear)
        private int OP_F2()
        {
            if (_flags!.P == 1)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // DI — Disable interrupts
        private int OP_F3()
        {
            _registers.IntEnable = false;
            return 4;
        }

        // CP — Call if positive (sign flag clear)
        private int OP_F4()
        {
            if (_flags!.S == 0)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // PUSH PSW — Push A and flags onto stack
        private int OP_F5()
        {
            _memory.WriteByte(_registers.SP - 1, _registers.A);
            byte addr = ((byte)(_flags!.Z | _flags!.S << 1 | _flags!.P << 2 | _flags!.CY << 3 | _flags!.AC << 4));
            _memory.WriteByte(_registers.SP - 2, addr);
            _registers.SP -= 2;
            return 11;
        }

        // ORI — OR immediate byte with A
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

        // RST 6 — Restart (call 0x0030)
        private int OP_F7()
        {
            Call(0x30, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }

        // RM — Return if minus (sign flag set)
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

        // SPHL — Load SP from HL
        private int OP_F9()
        {
            _registers.SP = (ushort)_registers.HL;
            return 5;
        }

        // JM — Jump if minus (sign flag set)
        private int OP_FA()
        {
            if (_flags!.S == 1)
            {
                ushort addr = ReadOpcodeDataWord();
                _registers.PC = addr;
                _registers.PC--;
            }
            else
            {
                _registers.PC += 2;
            }
            return 10;
        }

        // EI — Enable interrupts
        private int OP_FB()
        {
            _registers.IntEnable = true;
            return 4;
        }

        // CM — Call if minus (sign flag set)
        private int OP_FC()
        {
            if (_flags!.S == 1)
            {
                ushort addr = ReadOpcodeDataWord();
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

        // CPI — Compare immediate byte with A
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

        // RST 7 — Restart (call 0x0038)
        private int OP_FF()
        {
            Call(0x38, (ushort)(_registers.PC + 2));
            _registers.PC--;
            return 11;
        }
    }
}
