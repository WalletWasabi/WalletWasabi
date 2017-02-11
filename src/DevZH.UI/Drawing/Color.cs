using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Color
    {
        public double R;
        public double G;
        public double B;
        public double A;

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !(left == right);
        }

        public bool Equals(Color other)
        {
            return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Color && Equals((Color)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = R.GetHashCode();
                hashCode = (hashCode * 397) ^ G.GetHashCode();
                hashCode = (hashCode * 397) ^ B.GetHashCode();
                hashCode = (hashCode * 397) ^ A.GetHashCode();
                return hashCode;
            }
        }

        public static Color FromUint(uint argb)
        {
            var color = new Color();
            var a = (byte)((argb & 0xff000000) >> 24);
            var r = (byte)((argb & 0x00ff0000) >> 16);
            var g = (byte)((argb & 0x0000ff00) >> 8);
            var b = (byte)(argb & 0x000000ff);
            color.A = sRgbToScRgb(a);
            color.R = sRgbToScRgb(r);
            color.G = sRgbToScRgb(g);
            color.B = sRgbToScRgb(b);
            return color;
        }

        private static float sRgbToScRgb(byte bval)
        {
            float num = (float)bval / 255f;
            if ((double)num <= 0.0)
            {
                return 0f;
            }
            if ((double)num <= 0.04045)
            {
                return num / 12.92f;
            }
            if (num < 1f)
            {
                return (float)Math.Pow(((double)num + 0.055) / 1.055, 2.4);
            }
            return 1f;
        }

        public static explicit operator SolidColorBrush(Color color)
        {
            return new SolidColorBrush(color);
        }
    }
}
