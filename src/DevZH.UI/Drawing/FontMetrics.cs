using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FontMetrics
    {
        public double Ascent;
        public double Descent;
        public double Leading;
        // TODO do these two mean the same across all platforms?
        public double UnderlinePos;
        public double UnderlineThickness;
    }
}
