using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class MessageBox
    {
        public static void Show(Window owner, string title, string description, MessageBoxTypes mbTypes = MessageBoxTypes.Info)
        {
            var t = StringUtil.GetBytes(title);
            var c = StringUtil.GetBytes(description);
            switch (mbTypes)
            {
                case MessageBoxTypes.Info:
                    NativeMethods.MsgBox(owner.handle, t, c);
                    break;
                case MessageBoxTypes.Error:
                    NativeMethods.MsgBoxError(owner.handle, t, c);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mbTypes), mbTypes, null);
            }
        }

        public static void Show(string title, string description = ".NET Core wapper of libui", MessageBoxTypes mbTypes = MessageBoxTypes.Info)
        {
            Show(Application.MainWindow, title, description, mbTypes);
        }
    }
}
