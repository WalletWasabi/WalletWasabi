using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GradientStop
    {
        public double Pos;
        public double R;
        public double G;
        public double B;
        public double A;
    }
}
