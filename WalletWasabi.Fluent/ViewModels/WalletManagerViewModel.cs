using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels;

public partial class WalletManagerViewModel : ViewModelBase
{
	public WalletManagerViewModel(UiContext uiContext)
	{
		UiContext = uiContext;

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

							MainViewModel.Instance.NavBar.SelectedWallet = MainViewModel.Instance.NavBar.Wallets.FirstOrDefault(x => x.Wallet == wallet);
							wvm.NavigateAndHighlight(e.Transaction.GetHash());
						}

						NotificationHelpers.Show(wallet, e, OnClick);
					}

					if (walletViewModel.IsSelected && (e.NewlyReceivedCoins.Any() || e.NewlyConfirmedReceivedCoins.Any()))
					{
						await Task.Delay(200);
						wvm.History.SelectTransaction(e.Transaction.GetHash());
					}
				}
			});
	}

	public WalletViewModel GetWalletViewModel(Wallet wallet)
	{
		if (TryGetWalletViewModel(wallet, out var walletViewModel) && walletViewModel.WalletViewModel is { } result)
		{
			return result;
		}

		throw new Exception("Wallet not found, invalid api usage");
	}

	private bool TryGetWalletViewModel(Wallet wallet, [NotNullWhen(true)] out WalletPageViewModel? walletViewModel)
	{
		// TODO: Temporary Workaround
		// Remove soon
		walletViewModel =
			MainViewModel.Instance.NavBar.Wallets
										 .FirstOrDefault(x => x.Wallet.WalletName == wallet.WalletName);
		return walletViewModel is { };
	}
}
