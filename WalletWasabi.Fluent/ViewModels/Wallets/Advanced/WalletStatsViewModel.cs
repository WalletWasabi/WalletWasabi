using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(Title = "Wallet Stats")]
public partial class WalletStatsViewModel : RoutableViewModel
{
	public WalletStatsViewModel(WalletViewModelBase walletViewModelBase)
	{
		var wallet = walletViewModelBase.Wallet;

		// Number of coins in the wallet.
		// Changes to wallet.WalletRelevantTransactionProcessed
		var coinCount = wallet.Coins.Unspent().Count();

		// Total amount of money in the wallet.
		// Changes to wallet.WalletRelevantTransactionProcessed
		var balance = wallet.Coins.Unspent().TotalAmount();

		// Total amount of confirmed money in the wallet.
		// Changes to wallet.WalletRelevantTransactionProcessed
		var confirmedBalance = wallet.Coins.Confirmed().TotalAmount();

		// Total amount of unconfirmed money in the wallet.
		// Changes to wallet.WalletRelevantTransactionProcessed
		var unconfirmedBalance = wallet.Coins.Unconfirmed().TotalAmount();
	}
}
