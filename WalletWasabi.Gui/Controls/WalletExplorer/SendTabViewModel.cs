using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _coinList;

		public SendTabViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel)
		{
			CoinList = new CoinListViewModel(Global.WalletService.Coins);
		}

		public CoinListViewModel CoinList
		{
			get { return _coinList; }
			set { this.RaiseAndSetIfChanged(ref _coinList, value); }
		}
	}
}
