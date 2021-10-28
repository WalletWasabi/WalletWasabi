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
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
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
	public partial class SendFeeViewModel : DialogViewModelBase<FeeRate>
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;
		private readonly bool _isSilent;

		public SendFeeViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
		{
			_isSilent = isSilent;
			IsBusy = isSilent;
			_wallet = wallet;
			_transactionInfo = transactionInfo;

			FeeChart = new FeeChartViewModel();

			SetupCancel(false, true, false);
			EnableBack = true;

			NextCommand = ReactiveCommand.Create(() =>
		   {
			   _transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(FeeChart.CurrentConfirmationTarget);

			   Complete();
		   });
		}

		public FeeChartViewModel FeeChart { get; }

		private void Complete()
		{
			Close(DialogResultKind.Normal, new FeeRate(FeeChart.GetSatoshiPerByte(FeeChart.CurrentConfirmationTarget)));
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			IsBusy = true;

			base.OnNavigatedTo(isInHistory, disposables);

			var feeProvider = _wallet.FeeProvider;

			Observable
				.FromEventPattern(feeProvider, nameof(feeProvider.AllFeeEstimateChanged))
				.Select(x => (x.EventArgs as AllFeeEstimate)!.Estimations)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(estimations =>
				{
					FeeChart.UpdateFeeEstimates(TransactionFeeHelper.GetFeeEstimates(_wallet));
				})
				.DisposeWith(disposables);

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				while (feeProvider.AllFeeEstimate is null)
				{
					await Task.Delay(100);
				}

				FeeChart.UpdateFeeEstimates(TransactionFeeHelper.GetFeeEstimates(_wallet));

				if (_transactionInfo.FeeRate != FeeRate.Zero)
				{
					FeeChart.InitCurrentConfirmationTarget(_transactionInfo.FeeRate);
				}

				if (_isSilent)
				{
					var satPerByteThreshold = Services.Config.SatPerByteThreshold;
					var blockTargetThreshold = Services.Config.BlockTargetThreshold;
					var estimations = FeeChart.GetValues();

					var blockTarget = GetBestBlockTarget(estimations, satPerByteThreshold, blockTargetThreshold);

					FeeChart.CurrentConfirmationTarget = blockTarget;
					_transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(blockTarget);

					Complete();
				}
				else
				{
					IsBusy = false;
				}
			});
		}

		private double GetBestBlockTarget(Dictionary<double, double> estimations, int satPerByteThreshold, int blockTargetThreshold)
		{
			var possibleBlockTargets =
				estimations.OrderBy(x => x.Key).Where(x => x.Value <= satPerByteThreshold && x.Key <= blockTargetThreshold).ToArray();

			if (possibleBlockTargets.Any())
			{
				return possibleBlockTargets.First().Key;
			}

			return blockTargetThreshold;
		}

		private TimeSpan CalculateConfirmationTime(double targetBlock)
		{
			var timeInMinutes = Math.Ceiling(targetBlock) * 10;
			var time = TimeSpan.FromMinutes(timeInMinutes);
			return time;
		}
	}
}
