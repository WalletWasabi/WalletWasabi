using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class ComboBox : Control
    {
        public event EventHandler Selected;

        public ComboBox()
        {
            handle = NativeMethods.NewComboBox();
            InitializeEvents();
        }

        public void Add(params string[] text)
        {
            if (text == null)
            {
                NativeMethods.ComboBoxAppend(handle, StringUtil.GetBytes(null));
            }
            else
            {
                foreach (var s in text)
                {
                    NativeMethods.ComboBoxAppend(handle, StringUtil.GetBytes(s));
                }
            }
        }

        private int _index = -1;
        public int SelectedIndex
        {
            get
            {
                _index = NativeMethods.ComboBoxSelected(handle);
                return _index;
            }
            set
            {
                if (_index != value)
                {
                    NativeMethods.ComboBoxSetSelected(handle, value);
                    _index = value;
                }
            }
        }

        protected virtual void OnSelected(EventArgs e)
        {
            Selected?.Invoke(this, e);
        }

        protected void InitializeEvents()
        {
            NativeMethods.ComboBoxOnSelected(handle, (box, data) =>
            {
                OnSelected(EventArgs.Empty);
            }, IntPtr.Zero);
        }
    }
}
