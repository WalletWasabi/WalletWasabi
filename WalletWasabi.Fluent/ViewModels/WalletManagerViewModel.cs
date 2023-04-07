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
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels;

public partial class WalletManagerViewModel : ViewModelBase
{
	private readonly SourceList<NavBarWalletStateViewModel> _walletsSourceList = new();
	private readonly ReadOnlyObservableCollection<NavBarWalletStateViewModel> _wallets;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isLoadingWallet;

	public WalletManagerViewModel(UiContext uiContext)
	{
		UiContext = uiContext;

		Observable.Return(Unit.Default)
				  .Merge(
						Observable
							.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
							.Select(_ => Unit.Default))
			.ObserveOn(RxApp.MainThreadScheduler)
			.SelectMany(_ => Services.WalletManager.GetWallets())
			.Select(x => new NavBarWalletStateViewModel(x))
			.Distinct(x => x.Wallet)
			.ToObservableChangeSet()
 			.Bind(out _wallets)
			.Subscribe();

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

				if (TryGetWalletViewModel(wallet, out var walletViewModel) && walletViewModel?.WalletViewModel is { } wvm)
				{
					if (!e.IsOwnCoinJoin)
					{
						void OnClick()
						{
							if (MainViewModel.Instance.IsBusy)
							{
								return;
							}

							wvm.NavigateAndHighlight(e.Transaction.GetHash());
						}

						NotificationHelpers.Show(wallet, e, OnClick);
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

	public ReadOnlyObservableCollection<NavBarWalletStateViewModel> Wallets => _wallets;

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

	private bool TryGetWalletViewModel(Wallet wallet, [NotNullWhen(true)] out NavBarWalletStateViewModel? walletViewModel)
	{
		walletViewModel = Wallets.FirstOrDefault(x => x.Wallet == wallet);
		return walletViewModel is { };
	}
}
