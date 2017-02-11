using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct StrokeParamsInternal
    {
        public LineCap LineCap;
        public LineJoin LineJoin;
        public double Thickness;
        public double MiterLimit;
        public IntPtr Dashes;
        //[MarshalAs(UnmanagedType.SysUInt)]
        //public uint NumDashes;
        public UIntPtr NumDashes;
        public double DashPhase;
    }

    public class StrokeParams
    {
        internal StrokeParamsInternal Internal = new StrokeParamsInternal();

        public LineCap LineCap { get { return Internal.LineCap; } set { Internal.LineCap = value; } }
        public LineJoin LineJoin { get { return Internal.LineJoin; } set { Internal.LineJoin = value; } }
        public double Thickness { get { return Internal.Thickness; } set { Internal.Thickness = value; } }

        public double MiterLimit
        {
            get { return Internal.MiterLimit; }
            set { Internal.MiterLimit = value; }
        }

        public double[] Dashes
        {
            set
            {
                if (value != null && value.Length != 0)
                {
                    var length = value.Length;
                    Internal.Dashes = Marshal.UnsafeAddrOfPinnedArrayElement(value, 0);
                    Internal.NumDashes = (UIntPtr)length;
                }
            }
        }

        public double DashPhase { get { return Internal.DashPhase; } set { Internal.DashPhase = value; } }

        public StrokeParams()
        {
            MiterLimit = StrokeParamsValue.DefaultMiterLimit;
        }
    }

    public static class StrokeParamsValue
    {
        public const double DefaultMiterLimit = 10.0;
    }
}
