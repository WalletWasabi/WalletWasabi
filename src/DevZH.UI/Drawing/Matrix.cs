using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public class Matrix
    {
        public double M11;
        public double M12;
        public double M21;
        public double M22;
        public double M31;
        public double M32;

        public static Matrix SetIdentity()
        {
            Matrix matrix = new Matrix();
            NativeMethods.DrawMatrixSetIdentity(matrix);
            return matrix;
        }

        public static void Multiply([Out]  Matrix dest, [In]  Matrix src)
        {
            NativeMethods.DrawMatrixMultiply( dest,  src);
        }

        public void Translate(double x, double y)
        {
            NativeMethods.DrawMatrixTranslate( this, x, y);
        }

        public void Scale(double xCenter, double yCenter, double x, double y)
        {
            NativeMethods.DrawMatrixScale( this, xCenter, yCenter, x, y);
        }

        public void Rotate(double x, double y, double amount)
        {
            NativeMethods.DrawMatrixRotate( this, x, y, amount);
        }

        public void Skew(double x, double y, double xamount, double yamount)
        {
            NativeMethods.DrawMatrixSkew( this, x, y, xamount, yamount);
        }

        public void Multiply([In] ref Matrix src)
        {
            Multiply( this,  src);
        }

        public void Invertible()
        {
            NativeMethods.DrawMatrixInvertible( this);
        }

        public void Invert()
        {
            NativeMethods.DrawMatrixInvert( this);
        }

        public Point TransformToPoint()
        {
            var point = new Point();
            NativeMethods.DrawMatrixTransformPoint( this, out point.X, out point.Y);
            return point;
        }

        public Size TransformtoSize()
        {
            var size = new Size();
            NativeMethods.DrawMatrixTransformSize( this , out size._width, out size._height);
            return size;
        }
    }

    
}
