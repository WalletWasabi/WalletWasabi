using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Interface;
using DevZH.UI.Interop;

namespace DevZH.UI.Drawing
{
    public class DrawContext
    {
        public DrawContext(IntPtr ptr)
        {
            handle = ptr;
        }

        public void Fill(Path path, Brush brush)
        {
            var tmp = brush.Internal;
            NativeMethods.DrawFill(handle, path.ControlHandle, ref tmp);
        }

        public void Stroke(Path path, Brush brush, StrokeParams param)
        {
            var b = brush.Internal;
            var p = param.Internal;
            NativeMethods.DrawStroke(handle, path.ControlHandle, ref b, ref p);
        }

        public void Clip(Path path)
        {
            NativeMethods.DrawClip(handle, path.ControlHandle);
        }

        public void Save()
        {
            NativeMethods.DrawSave(handle);
        }

        public void ReStore()
        {
            NativeMethods.DrawRestore(handle);
        }

        public void Transform(Matrix matrix)
        {
            NativeMethods.DrawTransform(handle, matrix);
        }

        public void DrawText(double x, double y, TextLayout layout)
        {
            NativeMethods.DrawText(handle, x, y, layout.handle);
        }

        protected IntPtr handle;
    }
}
