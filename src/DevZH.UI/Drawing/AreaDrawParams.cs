using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI.Drawing
{
    public class AreaDrawParams
    {
        public DrawContext Context { get; internal set; }
        public double AreaWidth { get; internal set; }
        public double AreaHeight { get; internal set; }

        public double ClipX { get; internal set; }
        public double ClipY { get; internal set; }
        public double ClipWidth { get; internal set; }
        public double ClipHeight { get; internal set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AreaDrawParamsInternal
    {
        public IntPtr Context;
        // TODO document that this is only defined for nonscrolling areas
        public double AreaWidth;
        public double AreaHeight;

        public double ClipX;
        public double ClipY;
        public double ClipWidth;
        public double ClipHeight;

        public static explicit operator AreaDrawParams(AreaDrawParamsInternal p)
        {
            var param = new AreaDrawParams
            {
                Context = new DrawContext(p.Context),
                AreaWidth = p.AreaWidth,
                AreaHeight = p.AreaHeight,
                ClipX = p.ClipX,
                ClipY = p.ClipY,
                ClipWidth = p.ClipWidth,
                ClipHeight = p.ClipHeight
            };
            return param;
        }
    }
}
