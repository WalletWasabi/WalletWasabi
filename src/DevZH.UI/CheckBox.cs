using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class CheckBox : ButtonBase
    {
        public event EventHandler Checked;
        public event EventHandler UnChecked;

        public CheckBox(string text) : base(text)
        {
            handle = NativeMethods.NewCheckBox(StringUtil.GetBytes(text));
            InitializeEvents();
        }

        public override string Text {
            get
            {
                return StringUtil.GetString(NativeMethods.CheckBoxText(this.handle));
            }
            set
            {
                NativeMethods.CheckBoxSetText(handle, StringUtil.GetBytes(value));
            }
        }

        public bool IsChecked
        {
            get { return NativeMethods.CheckBoxChecked(handle); }
            set { NativeMethods.CheckBoxSetChecked(handle, value);}
        }

        protected void InitializeEvents()
        {
            NativeMethods.CheckBoxOnToggled(handle, (checkbox, data) =>
            {
                OnToggle(EventArgs.Empty);
            }, IntPtr.Zero);
        }

        protected virtual void OnToggle(EventArgs e)
        {
            if (IsChecked)
            {
                Checked?.Invoke(this, e);
            }
            else
            {
                UnChecked?.Invoke(this, e);
            }
        }
    }
}
