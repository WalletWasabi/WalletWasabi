using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletListModel : ReactiveObject, IWalletListModel
{
	private ReadOnlyObservableCollection<IWalletModel> _walletCollection;
	[AutoNotify] private IWalletModel? _selectedWalletModel;

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
					  .Transform(x => x as IWalletModel)
					  .Bind(out _walletCollection);

		SelectedWallet = this.WhenAnyValue(x => x.SelectedWalletModel);

		_selectedWalletModel =
			_walletCollection.FirstOrDefault(item => item.Name == Services.UiConfig.LastSelectedWallet)
			?? _walletCollection.FirstOrDefault();
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IObservable<IWalletModel?> SelectedWallet { get; }

	public void Select(IWalletModel wallet)
	{
		SelectedWalletModel = wallet;
	}
}
