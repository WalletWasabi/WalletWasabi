using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class Group : Control
    {
        public Group(string title)
        {
            handle = NativeMethods.NewGroup(StringUtil.GetBytes(title));
        }

        public string Title
        {
            get { return StringUtil.GetString(NativeMethods.GroupTitle(handle)); }
            set { NativeMethods.GroupSetTitle(handle, StringUtil.GetBytes(value));}
        }

        public bool AllowMargins
        {
            get { return NativeMethods.GroupMargined(handle); }
            set { NativeMethods.GroupSetMargined(handle, value);}
        }

        private Control _child;
        public Control Child
        {
            get { return _child; }
            set
            {
                if (_child != value && value.Verify())
                {
                    NativeMethods.GroupSetChild(handle, value.handle);
                    _child = value;
                }
            }
        }
    }
}
