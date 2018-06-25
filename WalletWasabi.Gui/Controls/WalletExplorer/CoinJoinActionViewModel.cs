using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinActionViewModel : WalletActionViewModel
	{
		public CoinJoinActionViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel) { }
	}
}
