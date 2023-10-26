using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Notifications;

public partial class WalletNotificationsViewModel : ViewModelBase
{
	private readonly IWalletSelector _walletSelector;
	[AutoNotify] private bool _isBusy;

	private WalletNotificationsViewModel(IWalletSelector walletSelector)
	{
		_walletSelector = walletSelector;
	}

	public void StartListening()
	{
		UiContext.WalletRepository.Wallets
								  .AutoRefresh(x => x.Auth.IsLoggedIn)
								  .Filter(x => x.Auth.IsLoggedIn)
								  .MergeMany(x => x.Transactions.NewTransactionArrived)
								  .Where(x => !UiContext.ApplicationSettings.PrivacyMode)
								  .Where(x => x.EventArgs.IsNews)
								  .Where(x => !IsBusy)
								  .DoAsync(x => OnNotificationReceivedAsync(x.Wallet, x.EventArgs))
								  .Subscribe();
	}

	private async Task OnNotificationReceivedAsync(IWalletModel wallet, ProcessedResult e)
	{
		if (!e.IsOwnCoinJoin)
		{
			void OnClick()
			{
				var wvm = _walletSelector.To(wallet);
				wvm?.SelectTransaction(e.Transaction.GetHash());
			}

			NotificationHelpers.Show(wallet, e, OnClick);
		}

		if (_walletSelector.SelectedWalletModel == wallet && (e.NewlyReceivedCoins.Any() || e.NewlyConfirmedReceivedCoins.Any()))
		{
			await Task.Delay(200);
			_walletSelector.SelectedWallet?.SelectTransaction(e.Transaction.GetHash());
		}
	}
}
