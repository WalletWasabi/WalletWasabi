using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
			var transactionInfo = _transactionInfo;

			Close(DialogResultKind.Normal, new FeeRate(FeeChart.GetSatoshiPerByte(FeeChart.CurrentConfirmationTarget)));
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
				// TODO What to do? Perhaps wait for fee provider to be updated.
			}

			if (_isSilent)
			{
				// TODO implement algorithm to intelligently select fees.
				_transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(1);

				Complete();
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
