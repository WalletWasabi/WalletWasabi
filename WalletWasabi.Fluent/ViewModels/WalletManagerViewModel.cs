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
	private readonly SourceList<WalletViewModelBase> _walletsSourceList = new();
	private readonly ObservableCollectionExtended<WalletViewModelBase> _wallets = new();

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isLoadingWallet;

	public WalletManagerViewModel()
	{
		_walletsSourceList
			.Connect()
			.Sort(SortExpressionComparer<WalletViewModelBase>.Descending(i => i.IsLoggedIn).ThenByAscending(i => i.Title))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(_wallets)
			.Subscribe();

		Observable
			.FromEventPattern<WalletState>(Services.WalletManager, nameof(WalletManager.WalletStateChanged))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x.Sender as Wallet)
			.WhereNotNull()
			.Subscribe(wallet =>
			{
				if (!TryGetWalletViewModel(wallet, out var walletViewModel))
				{
					return;
				}

				if (wallet.State == WalletState.Stopping)
				{
					RemoveWallet(walletViewModel);
				}
				else if (walletViewModel is ClosedWalletViewModel { IsLoggedIn: true } cwvm &&
						 ((cwvm.Wallet.KeyManager.SkipSynchronization && cwvm.Wallet.State == WalletState.Starting) ||
						  cwvm.Wallet.State == WalletState.Started))
				{
					OpenClosedWallet(cwvm);
				}
			});

		Observable
			.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(wallet =>
			{
				WalletViewModelBase vm = (wallet.State <= WalletState.Starting)
					? ClosedWalletViewModel.Create(wallet)
					: WalletViewModel.Create(wallet);

				InsertWallet(vm);
			});

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

				if (TryGetWalletViewModel(wallet, out var walletViewModel) && walletViewModel is WalletViewModel wvm)
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

					if (wvm.IsSelected && (e.NewlyReceivedCoins.Any() || e.NewlyConfirmedReceivedCoins.Any()))
					{
						await Task.Delay(200);
						wvm.History.SelectTransaction(e.Transaction.GetHash());
					}
				}
			});

		EnumerateWallets();
	}

	public ObservableCollection<WalletViewModelBase> Wallets => _wallets;

	public WalletViewModel GetWalletViewModel(Wallet wallet)
	{
		if (TryGetWalletViewModel(wallet, out var walletViewModel) && walletViewModel is WalletViewModel result)
		{
			return result;
		}

		throw new Exception("Wallet not found, invalid api usage");
	}

	public async Task LoadWalletAsync(Wallet wallet)
	{
		if (wallet.State != WalletState.Uninitialized)
		{
			throw new Exception("Wallet is already being logged in.");
		}

		try
		{
			await Task.Run(async () => await Services.WalletManager.StartWalletAsync(wallet));
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogTrace(ex);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private void OpenClosedWallet(ClosedWalletViewModel closedWalletViewModel)
	{
		IsLoadingWallet = true;

		RemoveWallet(closedWalletViewModel);

		var walletViewModelItem = OpenWallet(closedWalletViewModel.Wallet);

		if (closedWalletViewModel.IsSelected && walletViewModelItem.OpenCommand.CanExecute(default))
		{
			walletViewModelItem.OpenCommand.Execute(default);
		}

		IsLoadingWallet = false;
	}

	private WalletViewModel OpenWallet(Wallet wallet)
	{
		if (Wallets.Any(x => x.Title == wallet.WalletName))
		{
			throw new Exception("Wallet already opened.");
		}

		var walletViewModel = WalletViewModel.Create(wallet);

		InsertWallet(walletViewModel);

		return walletViewModel;
	}

	private void InsertWallet(WalletViewModelBase wallet)
	{
		_walletsSourceList.Add(wallet);
	}

	private void RemoveWallet(WalletViewModelBase walletViewModel)
	{
		_walletsSourceList.Remove(walletViewModel);
	}

	private void EnumerateWallets()
	{
		foreach (var wallet in Services.WalletManager.GetWallets())
		{
			InsertWallet(ClosedWalletViewModel.Create(wallet));
		}

		var walletToSelect = Wallets.FirstOrDefault(item => item.WalletName == Services.UiConfig.LastSelectedWallet) ?? Wallets.FirstOrDefault();

		if (walletToSelect is { } && walletToSelect.OpenCommand.CanExecute(default))
		{
			walletToSelect.OpenCommand.Execute(default);
		}
	}

	private bool TryGetWalletViewModel(Wallet wallet, [NotNullWhen(true)] out WalletViewModelBase? walletViewModel)
	{
		walletViewModel = Wallets.FirstOrDefault(x => x.Wallet == wallet);
		return walletViewModel is { };
	}
}
