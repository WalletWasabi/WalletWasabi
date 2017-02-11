using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class SaveFileDialog
    {
        private Window _parent;

        public string Path { get; private set; }

        public SaveFileDialog(Window parent = null)
        {
            _parent = parent ?? Application.MainWindow;
        }

        public bool Show()
        {
            Path = StringUtil.GetString(NativeMethods.SaveFile(_parent.handle));
            if (string.IsNullOrEmpty(Path))
            {
                return false;
            }
            return true;
        }

        public Stream OpenFile()
        {
            return File.OpenWrite(Path);
        }
    }
}
