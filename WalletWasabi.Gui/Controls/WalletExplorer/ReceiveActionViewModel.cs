using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveActionViewModel : WalletActionViewModel
	{
		public ReceiveActionViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel) { }
	}
}
