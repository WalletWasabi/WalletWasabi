using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interface;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class SpinBox : Control
    {
        public event EventHandler ValueChanged;

        private int _value;
        public int Value
        {
            get
            {
                _value = NativeMethods.SpinBoxValue(handle);
                return _value;
            }
            set
            {
                if (_value != value)
                {
                    NativeMethods.SpinBoxSetValue(handle, value);
                    _value = value;
                }
            }
        }

        public SpinBox(int min, int max)
        {
            handle = NativeMethods.NewSpinBox(min, max);
            InitializeEvents();
        }

        protected virtual void OnValueChanged(EventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }

        protected void InitializeEvents()
        {
            NativeMethods.SpinBoxOnChanged(handle, (box, data) =>
            {
                OnValueChanged(EventArgs.Empty);
            }, IntPtr.Zero);
        }
    }
}
