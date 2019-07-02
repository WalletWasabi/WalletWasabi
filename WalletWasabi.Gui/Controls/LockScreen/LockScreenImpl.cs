using Avalonia.Controls;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    internal class LockScreenImpl : UserControl
    {
		internal virtual void Reset()
		{
            IsHitTestVisible = true;
		}
    }
}
