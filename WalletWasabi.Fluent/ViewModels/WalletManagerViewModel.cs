using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels;

public partial class WalletManagerViewModel : ViewModelBase
{
	private readonly ReadOnlyObservableCollection<WalletPageViewModel> _wallets;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isLoadingWallet;

	public WalletManagerViewModel()
	{
		// Convert the Wallet Manager's contents into an observable stream.
		var walletsObservable = Observable.Return(Unit.Default)
			.Merge(
				Observable
					.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
					.Select(_ => Unit.Default))
			.ObserveOn(RxApp.MainThreadScheduler)
			.SelectMany(_ => Services.WalletManager.GetWallets());

		walletsObservable
			// Important to keep this key property so DynamicData knows.
			.ToObservableChangeSet(x => x.WalletName)
			// This converts the Wallet objects into WalletPageViewModel.
			.TransformWithInlineUpdate(newWallet => new WalletPageViewModel(newWallet),
				(e, wallet) => e.Wallet = wallet)
			// Refresh the collection when logged in.
			.AutoRefresh(x => x.IsLoggedIn)
			// Sort the list to put the most recently logged in wallet to the top.
			.Sort(SortExpressionComparer<WalletPageViewModel>
				.Descending(i => i.IsLoggedIn)
				.ThenByAscending(x => x.Title))
			.Bind(out _wallets)
			.Subscribe();

		Observable
			.FromEventPattern<ProcessedResult>(Services.WalletManager,
				nameof(Services.WalletManager.WalletRelevantTransactionProcessed))
			.ObserveOn(RxApp.MainThreadScheduler)
			.SubscribeAsync(async arg =>
			{
				var (sender, e) = arg;

				if (Services.UiConfig.PrivacyMode ||
				    !e.IsNews ||
				    sender is not Wallet { IsLoggedIn: true, State: WalletState.Started } wallet)
				{
					return;
				}

				if (TryGetWalletViewModel(wallet, out var walletViewModel) &&
				    walletViewModel?.WalletViewModel is { } wvm)
				{
					if (!e.IsOwnCoinJoin)
					{
						NotificationHelpers.Show(wallet.WalletName, e, onClick: () =>
						{
							if (MainViewModel.Instance.IsBusy)
							{
								return;
							}

							wvm.NavigateAndHighlight(e.Transaction.GetHash());
						});
					}

					if (walletViewModel.IsSelected &&
					    (e.NewlyReceivedCoins.Any() || e.NewlyConfirmedReceivedCoins.Any()))
					{
						await Task.Delay(200);
						wvm.History.SelectTransaction(e.Transaction.GetHash());
					}
				}
			});
	}

	public ReadOnlyObservableCollection<WalletPageViewModel> Wallets => _wallets;

	public bool TryGetSelectedAndLoggedInWalletViewModel([NotNullWhen(true)] out WalletViewModel? walletViewModel)
	{
		walletViewModel = Wallets.FirstOrDefault(x => x.IsSelected)?.WalletViewModel;
		return walletViewModel is { };
	}

	public WalletViewModel GetWalletViewModel(Wallet wallet)
	{
		if (TryGetWalletViewModel(wallet, out var walletViewModel) && walletViewModel.WalletViewModel is { } result)
		{
			return result;
		}

		throw new Exception("Wallet not found, invalid api usage");
	}

	private bool TryGetWalletViewModel(Wallet wallet,
		[NotNullWhen(true)] out WalletPageViewModel? walletViewModel)
	{
		walletViewModel = Wallets.FirstOrDefault(x => x.Wallet == wallet);
		return walletViewModel is { };
	}
}
