using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class ProgressBar : Control
    {
        public ProgressBar()
        {
            handle = NativeMethods.NewProgressBar();
        }

        private int _value;
        public int Value
        {
            get
            {
                _value = NativeMethods.ProgressBarValue(handle);
                return _value;
            }
            set
            {
                if (_value != value)
                {
                    NativeMethods.ProgressBarSetValue(handle, value);
                    _value = value;
                }
            }
        }
    }
}
