using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Drawing;
using DevZH.UI.Interface;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class FontPicker : Control
    {
        public event EventHandler FontChanged;

        private Font _font;
        public Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = new Font();
                }
                _font.handle = NativeMethods.FontButtonFont(handle);
                return _font;
            }
        }

        public FontPicker()
        {
            this.handle = NativeMethods.NewFontButton();
            InitializeEvents();
        }


        protected void InitializeEvents()
        {
            NativeMethods.FontButtonOnChanged(handle, (button, data) =>
            {
                OnFontChanged(EventArgs.Empty);
            }, IntPtr.Zero);
        }

        protected virtual void OnFontChanged(EventArgs e)
        {
            FontChanged?.Invoke(this, e);
        }
    }
}
