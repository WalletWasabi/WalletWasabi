using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Events;
using DevZH.UI.Interface;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class MenuItem : Control
    {
        internal MenuItem(IntPtr handle, MenuItemTypes type)
        {
            this.handle = handle;
            Type = type;
            InitializeEvents();
        }

        public MenuItemTypes Type { get; }

        private bool _enabled = true;
        public override bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                if (value)
                {
                    NativeMethods.MenuItemEnable(handle);
                }
                else
                {
                    NativeMethods.MenuItemDisable(handle);
                }
                _enabled = value;
            }
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get
            {
                if (this.Type == MenuItemTypes.Check)
                {
                    _isChecked = NativeMethods.MenuItemChecked(handle);
                }
                return _isChecked;
            }
            set
            {
                if (_isChecked != value && this.Type == MenuItemTypes.Check)
                {
                    NativeMethods.MenuItemSetChecked(handle, value);
                    _isChecked = value;
                }
            }
        }

        public void InitializeEvents()
        {
            switch (Type)
            {
                case MenuItemTypes.Quit:
                    break;
                default:
                    NativeMethods.MenuItemOnClicked(handle, (item, window, data) =>
                    {
                        OnClick(new DataEventArgs(window));
                    }, IntPtr.Zero);
                    break;
            }
        }
    }
}
