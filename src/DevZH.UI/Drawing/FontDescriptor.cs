using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Utils;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FontDescriptor
    {
        public byte[] Family;
        public double Size;
        public FontWeight Weight;
        public FontStyle Style;
        public FontStretch Stretch;

        public FontDescriptor(string family)
        {
            Family = StringUtil.GetBytes(family);
            Size = default(double);
            Weight = FontWeight.Thin;
            Style = FontStyle.Normal;
            Stretch = FontStretch.UltraCondensed;
        }
    }
}
