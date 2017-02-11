using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class MultilineEntry : Control
    {
        public bool IsWrapping { get; }

        private string _text;
        public override string Text
        {
            get
            {
                _text = StringUtil.GetString(NativeMethods.MultilineEntryText(handle));
                return _text;
            }
            set
            {
                if (_text != value)
                {
                    NativeMethods.MultilineEntrySetText(handle, StringUtil.GetBytes(value));
                    _text = value;
                }
            }
        }

        private bool _isReadOnly;
        public bool IsReadOnly
        {
            get
            {
                _isReadOnly = NativeMethods.MultilineEntryReadOnly(handle);
                return _isReadOnly;
            }
            set
            {
                if (_isReadOnly != value)
                {
                    NativeMethods.MultilineEntrySetReadOnly(handle, value);
                    _isReadOnly = value;
                }
            }
        }

        public MultilineEntry(bool isWrapping = true)
        {
            IsWrapping = isWrapping;
            if (isWrapping)
            {
                handle = NativeMethods.NewMultilineEntry();
            }
            else
            {
                handle = NativeMethods.NewNonWrappingMultilineEntry();
            }
        }

        public void Append(string append)
        {
            if (!string.IsNullOrEmpty(append))
            {
                NativeMethods.MultilineEntryAppend(handle, StringUtil.GetBytes(append));
            }
        }
    }
}
