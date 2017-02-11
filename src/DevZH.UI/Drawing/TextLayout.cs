using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interface;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI.Drawing
{
    public class TextLayout
    {
        public IntPtr handle;

        public double Width
        {
            set
            {
                NativeMethods.DrawTextLayoutSetWidth(handle, value);
            }
        }

        private readonly Metrics _extents = new Metrics();
        public Metrics Extents
        {
            get
            {
                double width, height;
                NativeMethods.DrawTextLayoutExtents(handle, out width, out height);
                _extents.Width = width;
                _extents.Height = height;
                return _extents;
            }
        }

        public TextLayout(string text, Font defaultFont, double width)
        {
            handle = NativeMethods.DrawNewTextLayout(StringUtil.GetBytes(text), defaultFont.handle, width);
        }

        public void Free()
        {
            NativeMethods.DrawFreeTextLayout(handle);
        }

        public void SetColor(int begin, int end, Color color)
        {
            NativeMethods.DrawTextLayoutSetColor(handle, begin, end, color.R, color.G, color.B, color.A);   
        }
    }
}
