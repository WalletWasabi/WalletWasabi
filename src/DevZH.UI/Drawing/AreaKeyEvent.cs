using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AreaKeyEvent
    {
        public byte Key;
        public ExtKey ExtKey;
        public Modifiers Modifier;
        public Modifiers Modifiers;
        public bool Up;
    }
}
