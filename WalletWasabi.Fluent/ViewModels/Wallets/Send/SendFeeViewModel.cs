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

			NextCommand = ReactiveCommand.Create( () =>
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
					FeeChart.UpdateFeeEstimates(_wallet.Network == Network.TestNet ? TestNetFeeEstimates : estimations);
				})
				.DisposeWith(disposables);

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				while (feeProvider.AllFeeEstimate is null)
				{
					await Task.Delay(100);
				}

				var feeEstimations = _wallet.Network == Network.TestNet ? TestNetFeeEstimates : feeProvider.AllFeeEstimate.Estimations;
				FeeChart.UpdateFeeEstimates(feeEstimations);

				if (_transactionInfo.FeeRate is { })
				{
					FeeChart.InitCurrentConfirmationTarget(_transactionInfo.FeeRate);
				}

				if (_isSilent)
				{
					var blockTarget = GetBestBlockTarget(feeEstimations);

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

		private int GetBestBlockTarget(Dictionary<int, int> estimations)
		{
			var satPerByteThreshold = 10;
			var blockTargetThreshold = 6;

			var bestBlockTarget = estimations.FirstOrDefault(x => x.Value < satPerByteThreshold && x.Key < blockTargetThreshold);

			if (bestBlockTarget.Key != default && bestBlockTarget.Value != default)
			{
				return bestBlockTarget.Value;
			}

			return blockTargetThreshold;
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
