using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace HiddenWallet.UserInterface.Controls
{
    class CueTextBox : TextBox
    {
        [Localizable(true)]
        internal string Cue
        {
            get { return _mCue; }
            set { _mCue = value; UpdateCue(); }
        }

        private void UpdateCue()
        {
            if (IsHandleCreated && _mCue != null)
            {
                SendMessage(Handle, 0x1501, (IntPtr)1, _mCue);
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateCue();
        }
        private string _mCue;

        // PInvoke
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, string lp);
    }
}
