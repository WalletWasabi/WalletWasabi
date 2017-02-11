using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class Label : Control
    {
        public Label(string text)
        {
            base.Text = text;
            handle = NativeMethods.NewLabel(StringUtil.GetBytes(text));
        }

        public override string Text
        {
            get
            {
                base.Text = StringUtil.GetString(NativeMethods.LabelText(handle));
                return base.Text;
            }
            set
            {
                if (base.Text != value)
                {
                    NativeMethods.LabelSetText(handle, StringUtil.GetBytes(value));
                    base.Text = value;
                }
            }
        }
    }
}
