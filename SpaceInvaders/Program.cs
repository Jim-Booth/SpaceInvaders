﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceInvaders
{
    internal class Program
    {
        static _8080CPU cpu;

        static void Main(string[] args)
        {
            cpu = new _8080CPU();
            cpu.ReadROM(@"invaders");
        }

    }
}
