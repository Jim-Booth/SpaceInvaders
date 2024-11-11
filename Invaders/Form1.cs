using SpaceInvaders;

namespace Invaders
{
    public partial class Form1 : Form
    {
        private _8080CPU? cpu;
        private Thread? cpu_thread;
        private Thread? display_thread;

        public Form1()
        {
            InitializeComponent();
        }

        private void RunDisplay()
        {
            while (cpu != null && cpu.Running)
            {
                while (!cpu.DisplayReady)
                    if (!cpu.Running) { break; }
                if (!cpu.Running) { break; }
                byte[] video = cpu.GetVideoRam();
                Bitmap videoBitmap = new(224, 256);
                int ptr = 0;
                for (int x = 0; x < 224; x++)
                {
                    for (int y = 255; y > 0; y -= 8)
                    {
                        byte value = video[ptr++];
                        for (int b = 0; b < 8; b++)
                        {
                            videoBitmap.SetPixel(x, y - b, Color.DarkBlue);
                            bool bit = (value & (1 << b)) != 0;
                            if ((value & (1 << b)) != 0)
                                videoBitmap.SetPixel(x, y - b, Color.White);
                        }
                    }
                }
                pictureBox1.Invoke((MethodInvoker)delegate { pictureBox1.Image = videoBitmap; });
            }
        }

        private void Execute()
        {
            cpu = new _8080CPU();
            //cpu.paused = true;
            cpu.ReadROM(@"invaders.rom");
            cpu_thread = new Thread(() => cpu.RunEmulation());
            cpu_thread.Start();
            while (!cpu.Running) { }
            display_thread = new Thread(() => RunDisplay());
            display_thread.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Execute();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            cpu!.Running = false;
            cpu!.paused = false;
            display_thread = null;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            cpu!.paused = true;
            PrintDebugInfo();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            cpu!.step = true;
            PrintDebugInfo();
        }

        private void PrintDebugInfo()
        {
            List<string> lines = new List<string>();
            lines.Add("OP=" + cpu!.Registers.memory[cpu.Registers.PC].ToString("X2"));
            lines.Add("PC=" + cpu!.Registers.PC.ToString("X2"));
            lines.Add("SP=" + cpu!.Registers.SP.ToString("X2"));
            lines.Add("A=" + cpu!.Registers.A.ToString("X2"));
            lines.Add("B=" + cpu!.Registers.B.ToString("X2"));
            lines.Add("C=" + cpu!.Registers.C.ToString("X2"));
            lines.Add("D=" + cpu!.Registers.D.ToString("X2"));
            lines.Add("E=" + cpu!.Registers.E.ToString("X2"));
            lines.Add("H=" + cpu!.Registers.H.ToString("X2"));
            lines.Add("L=" + cpu!.Registers.L.ToString("X2"));
            lines.Add("z=" + cpu!.Registers.Flags.Z.ToString("X2"));
            lines.Add("s=" + cpu!.Registers.Flags.S.ToString("X2"));
            lines.Add("p=" + cpu!.Registers.Flags.P.ToString("X2"));
            lines.Add("cy=" + cpu!.Registers.Flags.CY.ToString("X2"));
            lines.Add("ac=" + cpu!.Registers.Flags.AC.ToString("X2"));
            label2.Text = "";
            foreach (string line in lines)
                label2.Text += line + "\r\n";
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            button2_Click(sender, e);
        }
    }
}