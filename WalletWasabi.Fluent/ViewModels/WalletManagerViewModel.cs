using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
	private readonly SourceList<NavBarWalletStateViewModel> _walletsSourceList = new();
	private readonly ObservableCollectionExtended<NavBarWalletStateViewModel> _wallets = new();

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isLoadingWallet;

	public WalletManagerViewModel()
	{
		_walletsSourceList
			.Connect()
			.Sort(SortExpressionComparer<NavBarWalletStateViewModel>.Descending(i => i.IsLoggedIn)
				.ThenByAscending(i => i.Wallet.WalletName))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(_wallets)
			.Subscribe();

		//
		// Observable
		// 	.FromEventPattern<WalletState>(Services.WalletManager, nameof(WalletManager.WalletStateChanged))
		// 	.ObserveOn(RxApp.MainThreadScheduler)
		// 	.Select(x => x.Sender as Wallet)
		// 	.WhereNotNull()
		// 	.Subscribe(wallet =>
		// 	{
		// 		if (!TryGetWalletViewModel(wallet, out var walletViewModel))
		// 		{
		// 			return;
		// 		}
		//
		// 		// if (wallet.State == WalletState.Stopping)
		// 		// {
		// 		// 	RemoveWallet(walletViewModel);
		// 		// }
		// 		// else if (walletViewModel is ClosedWalletViewModel { IsLoggedIn: true } cwvm &&
		// 		// 		 ((cwvm.Wallet.KeyManager.SkipSynchronization && cwvm.Wallet.State == WalletState.Starting) ||
		// 		// 		  cwvm.Wallet.State == WalletState.Started))
		// 		// {
		// 		// 	OpenClosedWallet(cwvm);
		// 		// }
		// 	});

		// Observable
		// 	.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
		// 	.Select(x => x.EventArgs)
		// 	.ObserveOn(RxApp.MainThreadScheduler)
		// 	.Subscribe(wallet =>
		// 	{
		// 		InsertWallet(new NavBarWalletStateViewModel(wallet));
		// 	});

		Observable
			.FromEventPattern<ProcessedResult>(Services.WalletManager, nameof(Services.WalletManager.WalletRelevantTransactionProcessed))
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

				if (TryGetWalletViewModel(wallet, out var walletViewModel) &&  walletViewModel?.WalletViewModel is WalletViewModel wvm)
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

					if (walletViewModel.IsSelected && (e.NewlyReceivedCoins.Any() || e.NewlyConfirmedReceivedCoins.Any()))
					{
						await Task.Delay(200);
						wvm.History.SelectTransaction(e.Transaction.GetHash());
					}
				}
			});

		EnumerateWallets();
	}

	public ObservableCollection<NavBarWalletStateViewModel> Wallets => _wallets;

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

	// private void OpenClosedWallet(ClosedWalletViewModel closedWalletViewModel)
	// {
	// 	IsLoadingWallet = true;
	//
	// 	closedWalletViewModel.StopLoading();
	//
	// 	RemoveWallet(closedWalletViewModel);
	//
	// 	var walletViewModelItem = OpenWallet(closedWalletViewModel.Wallet);
	//
	// 	if (closedWalletViewModel.IsSelected && walletViewModelItem.OpenCommand.CanExecute(default))
	// 	{
	// 		walletViewModelItem.OpenCommand.Execute(default);
	// 	}
	//
	// 	IsLoadingWallet = false;
	// }
	//
	// private WalletViewModel OpenWallet(Wallet wallet)
	// {
	// 	if (Wallets.Any(x => x.Title == wallet.WalletName))
	// 	{
	// 		throw new Exception("Wallet already opened.");
	// 	}
	//
	// 	var walletViewModel = WalletViewModel.Create(wallet);
	//
	// 	InsertWallet(walletViewModel);
	//
	// 	return walletViewModel;
	// }

	private void InsertWallet(NavBarWalletStateViewModel wallet)
	{
		_walletsSourceList.Add(wallet);
	}

	private void RemoveWallet(NavBarWalletStateViewModel walletViewModel)
	{
		_walletsSourceList.Remove(walletViewModel);
	}

	private void EnumerateWallets()
	{
		foreach (var wallet in Services.WalletManager.GetWallets())
		{
			InsertWallet(new NavBarWalletStateViewModel(wallet));
		}
	}

	private bool TryGetWalletViewModel(Wallet wallet, [NotNullWhen(true)] out NavBarWalletStateViewModel? walletViewModel)
	{
		walletViewModel = Wallets.FirstOrDefault(x => x.Wallet == wallet);
		return walletViewModel is { };
	}
}
