using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletListModel : ReactiveObject, IWalletListModel
{
	[AutoNotify] private IWalletModel? _selectedWallet;

	public WalletListModel()
	{
		// Convert the Wallet Manager's contents into an observable stream of IWalletModels.
		Wallets =
			Observable.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded)).Select(_ => Unit.Default)
					  .StartWith(Unit.Default)
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .SelectMany(_ => Services.WalletManager.GetWallets())
					  .ToObservableChangeSet(x => x.WalletName)
					  .TransformWithInlineUpdate(wallet => new WalletModel(wallet), (model, wallet) => { })

					  // Refresh the collection when logged in.
					  .AutoRefresh(x => x.IsLoggedIn)
					  .Transform(x => x as IWalletModel);

		// Materialize the Wallet list to determine the default wallet.
		Wallets
			.Bind(out var wallets)
			.Subscribe();

		DefaultWallet =
			wallets.FirstOrDefault(item => item.Name == Services.UiConfig.LastSelectedWallet)
			?? wallets.FirstOrDefault();

		this.WhenAnyValue(x => x.SelectedWallet)
			.WhereNotNull()
			.Do(x => Services.UiConfig.LastSelectedWallet = x.Name)
			.Subscribe();
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IWalletModel? DefaultWallet { get; }
}
