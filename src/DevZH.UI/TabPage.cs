using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class TabPage : Control
    {
        public string Name { get; }

        protected Control _child;

        public Control Child
        {
            get { return _child; }
            protected set
            {
                if (_child != value)
                {
                    _child = value;
                    handle = _child.handle;
                }
            }
        }

        private bool _beforeAdd = true;
        private bool _allowMargins;
        public bool AllowMargins
        {
            get
            {
                if (Parent != null && !Parent.Verify())
                {
                    _allowMargins = NativeMethods.TabMargined(Parent.handle, Index);
                    _beforeAdd = false;
                }
                return _allowMargins;
            }
            set
            {
                if (_allowMargins != value)
                {
                    if (Parent != null && !Parent.Verify())
                    {
                        NativeMethods.TabSetMargined(Parent.handle, Index, value);
                    }
                    _allowMargins = value;
                }
            }
        }

        public TabPage(string name)
        {
            Name = name;
        }

        public TabPage(string name, Control child)
        {
            Name = name;
            Child = child;
        }

        protected internal override void DelayRender()
        {
            if (_beforeAdd && _allowMargins)
            {
                NativeMethods.TabSetMargined(Parent.handle, Index, _allowMargins);
            }
        }
    }
}
