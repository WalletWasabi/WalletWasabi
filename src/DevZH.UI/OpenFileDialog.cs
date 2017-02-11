using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class OpenFileDialog
    {
        public string Path { get; private set; }
        private Window _parent;

        public OpenFileDialog(Window parent = null)
        {
            _parent = parent ?? Application.MainWindow;
        }

        public bool Show()
        {
            Path = StringUtil.GetString(NativeMethods.OpenFile(_parent.handle));
            if (string.IsNullOrEmpty(Path))
            {
                return false;
            }
            return true;
        }
    }
}
