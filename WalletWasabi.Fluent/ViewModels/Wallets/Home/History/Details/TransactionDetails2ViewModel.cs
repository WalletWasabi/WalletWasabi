using System.Reactive;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetails2ViewModel : RoutableViewModel
{
	public TransactionDetails2ViewModel(TransactionSummary transactionSummary, Wallet wallet, IObservable<Unit> updateTrigger)
	{
		
	}
}
