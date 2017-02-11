using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public abstract class EntryBase : Control
    {
        protected EntryBase()
        {
            
        }

        public override string Text
        {
            get { return StringUtil.GetString(NativeMethods.EntryText(handle)); }
            set { NativeMethods.EntrySetText(handle, StringUtil.GetBytes(value));}
        }

        private bool _readonly;
        public bool IsReadOnly
        {
            get
            {
                _readonly = NativeMethods.EntryReadOnly(handle);
                return _readonly;
            }
            set
            {
                if (_readonly != value)
                {
                    NativeMethods.EntrySetReadOnly(handle, value);
                }
            }
        }

        protected void InitializeEvents()
        {
            NativeMethods.EntryOnChanged(handle, (entry, data) =>
            {
                OnTextChanged(EventArgs.Empty);
            }, IntPtr.Zero);
        }
    }
}
