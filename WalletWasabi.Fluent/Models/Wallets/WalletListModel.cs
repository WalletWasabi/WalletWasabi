using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

internal class WalletListModel : ReactiveObject, IWalletListModel
{
	public WalletListModel()
	{
		//Convert the Wallet Manager's contents into an observable stream.
		Wallets =
			Observable.Return(Unit.Default)
					  .Merge(Observable.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
									   .Select(_ => Unit.Default))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .SelectMany(_ => Services.WalletManager.GetWallets())
					  // Important to keep this key property so DynamicData knows.
					  .ToObservableChangeSet(x => x.WalletName)
					  // This converts the Wallet objects into WalletPageViewModel.
					  .TransformWithInlineUpdate(wallet => new WalletModel(wallet))
					  // Refresh the collection when logged in.
					  .AutoRefresh(x => x.IsLoggedIn)
					  // Sort the list to put the most recently logged in wallet to the top.
					  .Sort(SortExpressionComparer<IWalletModel>.Descending(i => i.IsLoggedIn).ThenByAscending(x => x.Name))
					  .Transform(x => x as IWalletModel);
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }
}
