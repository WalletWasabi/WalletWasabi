using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class SendFeeViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;
		private readonly bool _isSilent;
		private readonly FeeRate? _entryFeeRate;
		private double _lastConfirmationTarget;

		public SendFeeViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
		{
			_isSilent = isSilent;
			IsBusy = isSilent;
			_wallet = wallet;
			_transactionInfo = transactionInfo;
			_entryFeeRate = transactionInfo.FeeRate;

			FeeChart = new FeeChartViewModel();

			SetupCancel(false, true, false);
			EnableBack = true;

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				_lastConfirmationTarget = FeeChart.CurrentConfirmationTarget;
				_transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(_lastConfirmationTarget);

				await OnNextAsync();
			});
		}

		public FeeChartViewModel FeeChart { get; }

		private async Task OnNextAsync()
		{
			var transactionInfo = _transactionInfo;
			var targetAnonymitySet = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
			var mixedCoins = _wallet.Coins.Where(x => x.HdPubKey.AnonymitySet >= targetAnonymitySet).ToList();
			var totalMixedCoinsAmount = Money.FromUnit(mixedCoins.Sum(coin => coin.Amount), MoneyUnit.Satoshi);
			transactionInfo.Coins = mixedCoins;

			transactionInfo.FeeRate = new FeeRate(FeeChart.GetSatoshiPerByte(FeeChart.CurrentConfirmationTarget));

			if (transactionInfo.FeeRate == _entryFeeRate)
			{
				Navigate().Back();
				return;
			}

			if (transactionInfo.Amount > totalMixedCoinsAmount)
			{
				Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);
				return;
			}

			try
			{
				if (_transactionInfo.PayJoinClient is { })
				{
					await BuildTransactionAsPayJoinAsync(transactionInfo);
				}
				else
				{
					await BuildTransactionAsNormalAsync(transactionInfo, totalMixedCoinsAmount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(),
					"Wasabi was unable to create your transaction.");
			}
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			var feeProvider = _wallet.FeeProvider;

			Observable
				.FromEventPattern(feeProvider, nameof(feeProvider.AllFeeEstimateChanged))
				.Select(x => (x.EventArgs as AllFeeEstimate)!.Estimations)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(estimations =>
				{
					FeeChart.UpdateFeeEstimates(_wallet.Network == Network.TestNet ? TestNetFeeEstimates : estimations);
				})
				.DisposeWith(disposables);

			if (feeProvider.AllFeeEstimate is { })
			{
				FeeChart.UpdateFeeEstimates(_wallet.Network == Network.TestNet ? TestNetFeeEstimates : feeProvider.AllFeeEstimate.Estimations);

				if (_transactionInfo.FeeRate is { })
				{
					FeeChart.InitCurrentConfirmationTarget(_transactionInfo.FeeRate);
				}
			}
			else
			{
				// TODO What to do?
			}

			if (_isSilent)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					// TODO implement algorithm to intelligently select fees.
					_lastConfirmationTarget = 1;
					_transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(_lastConfirmationTarget);

					await OnNextAsync();
				});
			}
		}

		private async Task BuildTransactionAsNormalAsync(TransactionInfo transactionInfo, Money totalMixedCoinsAmount)
		{
			try
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, txRes), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);
			}
			catch (InsufficientBalanceException)
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo.Address,
					totalMixedCoinsAmount, transactionInfo.Labels, transactionInfo.FeeRate!, transactionInfo.Coins,
					subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Private, txRes,
					_wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, txRes), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);
				}
				else
				{
					Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);
				}
			}
		}

		private async Task BuildTransactionAsPayJoinAsync(TransactionInfo transactionInfo)
		{
			try
			{
				// Do not add the PayJoin client yet, it will be added before broadcasting.
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(_wallet, transactionInfo, txRes), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building",
					"There are not enough private funds to cover the transaction fee",
					"Wasabi was unable to create your transaction.");
				Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);
			}
		}

		private TimeSpan CalculateConfirmationTime(double targetBlock)
		{
			var timeInMinutes = Math.Ceiling(targetBlock) * 10;
			var time = TimeSpan.FromMinutes(timeInMinutes);
			return time;
		}

		private static readonly Dictionary<int, int> TestNetFeeEstimates = new ()
		{
			[1] = 17,
			[2] = 15,
			[3] = 11,
			[6] = 11,
			[18] = 9,
			[36] = 7,
			[72] = 5,
			[144] = 2,
			[432] = 1,
			[1008] = 1
		};
	}
}
