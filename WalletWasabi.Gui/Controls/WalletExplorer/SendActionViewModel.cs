using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendActionViewModel : WalletActionViewModel
	{
		public SendActionViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel) { }
	}
}
