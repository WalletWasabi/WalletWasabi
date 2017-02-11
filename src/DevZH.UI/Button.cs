using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevZH.UI.Interface;
using DevZH.UI.Interop;
using DevZH.UI.Utils;
using System.Reflection;

namespace DevZH.UI
{
    public class Button : ButtonBase
    {
        public override string Text
        {
            get { return StringUtil.GetString(NativeMethods.ButtonText(handle)); }
            set { NativeMethods.ButtonSetText(handle, StringUtil.GetBytes(value));}
        }

        public Button(string text) : base(text)
        {
            /*var result = this.GetType().GetTypeInfo().IsSubclassOf(typeof(Button));
            if (!result)
            {
                ControlHandle = NativeMethods.NewButton(StringUtil.GetBytes(text));
                InitializeEvents();
            }*/
            handle = NativeMethods.NewButton(StringUtil.GetBytes(text));
            InitializeEvents();
        }

        protected void InitializeEvents()
        {
            NativeMethods.ButtonOnClicked(handle, (button, data) =>
            {
                OnClick(EventArgs.Empty);
            }, IntPtr.Zero);
        }
    }
}
