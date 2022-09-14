using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetails2ViewModel : RoutableViewModel
{
	public TransactionDetails2ViewModel(RealTransaction transaction)
	{
		Amount = transaction.Amount.ObserveOn(RxApp.MainThreadScheduler).ReplayLastActive();
	}

	public IObservable<Money> Amount { get; }
}
