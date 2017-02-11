using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    // TODO document drag captures
    [StructLayout(LayoutKind.Sequential)]
    public struct AreaMouseEvent
    {
        // TODO document what these mean for scrolling areas
        public double X;
        public double Y;

        // TODO see draw above
        public double AreaWidth;
        public double AreaHeight;

        public int Down;
        public int Up;

        public int Count;

        public Modifiers Modifiers;

        public ulong Held1To64;
    }
}
