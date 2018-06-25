using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		public SendTabViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel) { }
	}
}
