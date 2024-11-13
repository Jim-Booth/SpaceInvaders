using Microsoft.Win32;
using SpaceInvaders;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;

namespace Invaders
{
    public partial class Form1 : Form
    {
        private _8080CPU? cpu;
        private Thread? emu_thread;
        private Thread? cpu_thread;
        private Thread? display_thread;
        private bool displayRunning = false;
        private byte[] ports = new byte[4];

        public Form1()
        {
            InitializeComponent();
            Execute();
        }

        private void Execute()
        {
            cpu = new _8080CPU();
            //cpu.paused = true;
            //cpu.ReadROM(@"cpudiag.bin", 256);
            cpu.ReadROM(@"invaders.rom", 0x0);
            emu_thread = new Thread(() => RunEmulation());
            emu_thread.Start();

            while (!cpu.Running) { }
            display_thread = new Thread(() => RunDisplay());
            display_thread.Start();
        }

        private void RunEmulation()
        {
            cpu_thread = new Thread(() => cpu!.RunEmulation());
            cpu_thread.IsBackground = true;
            cpu_thread.Start();
            while (!cpu!.Running) { }
            while (cpu.Running)
            {
                if (ports[1] > 0 || ports[2] > 0)
                    cpu.PortIn = ports;
                //Thread.Sleep(1);
            }
        }

        private void RunDisplay()
        {
            displayRunning = true;
            while (cpu != null && cpu.Running && displayRunning)
            {
                if (!cpu.Running) { break; }
                Bitmap videoBitmap = new(224, 256);
                int ptr = 0;
                for (int x = 0; x < 224; x++)
                {
                    for (int y = 255; y > 0; y -= 8)
                    {
                        Color color = GetPixelColor(x, y);
                        byte value = cpu.Video[ptr++];
                        for (int b = 0; b < 8; b++)
                        {
                            videoBitmap.SetPixel(x, y - b, Color.FromArgb(180, 0, 0, 0));
                            bool bit = (value & (1 << b)) != 0;
                            if ((value & (1 << b)) != 0)
                                videoBitmap.SetPixel(x, y - b, color);
                        }
                    }
                }
                try
                {
                    pictureBox1.Invoke((MethodInvoker)delegate { pictureBox1.Image = videoBitmap; });
                }
                catch { }
            }
        }

        private Color GetPixelColor(int x, int y)
        {
            if (y < 240 && y > 183) return Color.Green;
            if ((y < 256 && y > 239) && (x > 25 && x < 137)) return Color.Green;
            if (y < 64 && y > 31) return Color.Red;
            return Color.White;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cpu!.Running = false;
            cpu!.paused = false;
            displayRunning = false;
        }

        private byte GetKeyValue(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C) return 1;
            if (e.KeyCode == Keys.D1) return 2;
            if (e.KeyCode == Keys.D2) return 3;
            if (e.KeyCode == Keys.Left) return 4;
            if (e.KeyCode == Keys.Right) return 5;
            if (e.KeyCode == Keys.D) return 6;
            if (e.KeyCode == Keys.Z) return 7;
            if (e.KeyCode == Keys.X) return 8;
            if (e.KeyCode == Keys.LShiftKey) return 9;
            return 99;
        }

        private void KeyPressed(uint key)
        {
            switch (key)
            {
                case 1: // Coin
                    ports[1] |= 0x01;
                    break;
                case 2: // 1P Start
                    ports[1] |= 0x04;
                    break;
                case 3: // 2P start
                    ports[1] |= 0x02;
                    break;
                case 4: // 1P Left
                    ports[1] |= 0x20;
                    break;
                case 5: // 1P Right
                    ports[1] |= 0x40;
                    break;
                case 6: // 1P Fire
                    ports[1] |= 0x10;
                    break;
                case 7: // 2P Left
                    ports[2] |= 0x20;
                    break;
                case 8: // 2P Right
                    ports[2] |= 0x40;
                    break;
                case 9: // 2P Fire
                    ports[2] |= 0x10;
                    break;
            }
        }

        private void KeyLifted(uint key)
        {
            switch (key)
            {
                case 1: // Coin
                    ports[1] &= 0xFE;
                    break;
                case 2: // 1P Start
                    ports[1] &= 0xFB;
                    break;
                case 3: // 2P start
                    ports[1] &= 0xFD;
                    break;
                case 4: // 1P Left
                    ports[1] &= 0xDF;
                    break;
                case 5: // 1P Right
                    ports[1] &= 0xBF;
                    break;
                case 6: // 1P Fire
                    ports[1] &= 0xEF;
                    break;
                case 7: // 2P Left
                    ports[2] &= 0xDF;
                    break;
                case 8: // 2P Right
                    ports[2] &= 0xBF;
                    break;
                case 9: // 2P Fire
                    ports[2] &= 0xEF;
                    break;

            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            uint k = GetKeyValue(e);
            if (k != 99)
                KeyPressed(k);
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            uint k = GetKeyValue(e);
            if (k != 99)
                KeyLifted(k);
        }
    }
}
