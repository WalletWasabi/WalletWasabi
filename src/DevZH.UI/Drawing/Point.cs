using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public double X;
        public double Y;

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static bool operator ==(Point point1, Point point2)
        {
            return point1.X.Equals(point2.X) &&
                   point1.Y.Equals(point2.Y);
        }

        public static bool operator !=(Point point1, Point point2)
        {
            return !(point1 == point2);
        }

        public static bool Equals(Point point1, Point point2)
        {
            return point1.X.Equals(point2.X) &&
                   point1.Y.Equals(point2.Y);
        }

        public override bool Equals(object o)
        {
            if (!(o is Point))
            {
                return false;
            }

            Point value = (Point)o;
            return Point.Equals(this, value);
        }

        public bool Equals(Point value)
        {
            return Point.Equals(this, value);
        }

        public override int GetHashCode()
        {
            // Perform field-by-field XOR of HashCodes
            return X.GetHashCode() ^
                   Y.GetHashCode();
        }

        public static explicit operator Size(Point point)
        {
            return new Size(Math.Abs(point.X), Math.Abs(point.Y));
        }
    }
}
