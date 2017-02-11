using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class Slider : Control
    {
        public event EventHandler ValueChanged;

        private int _value;
        public int Value
        {
            get
            {
                _value = NativeMethods.SliderValue(handle);
                return _value;
            }
            set
            {
                if (_value != value)
                {
                    NativeMethods.SliderSetValue(handle, value);
                    _value = value;
                }
            }
        }

        public Slider(int min, int max)
        {
            handle = NativeMethods.NewSlider(min, max);
            InitializeEvents();
        }

        protected virtual void OnValueChanged(EventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }

        protected void InitializeEvents()
        {
            NativeMethods.SliderOnChanged(handle, (slider, data) =>
            {
                OnValueChanged(EventArgs.Empty);
            }, IntPtr.Zero);
        }
    }
}
