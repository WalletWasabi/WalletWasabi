using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel) { }
	}
}
