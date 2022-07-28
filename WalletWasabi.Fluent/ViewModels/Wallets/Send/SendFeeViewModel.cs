using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

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
		   _transactionInfo.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(FeeChart.CurrentConfirmationTarget);

		   Complete();
	   });
	}

	public FeeChartViewModel FeeChart { get; }

	private void Complete()
	{
		var blockTarget = FeeChart.CurrentConfirmationTarget;

		Services.UiConfig.FeeTarget = (int)blockTarget;
		Close(DialogResultKind.Normal, new FeeRate(FeeChart.GetSatoshiPerByte(blockTarget)));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		IsBusy = true;

		base.OnNavigatedTo(isInHistory, disposables);

		var feeProvider = _wallet.FeeProvider;

		Observable
			.FromEventPattern(feeProvider, nameof(feeProvider.AllFeeEstimateChanged))
			.Select(_ =>
			{
				TransactionFeeHelper.TryGetFeeEstimates(_wallet, out var estimates);
				return estimates;
			})
			.WhereNotNull()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(estimations => FeeChart.UpdateFeeEstimates(estimations, _transactionInfo.MaximumPossibleFeeRate))
			.DisposeWith(disposables);


		if (!TransactionFeeHelper.TryGetFeeEstimates(_wallet, out var feeEstimates))
		{
			// TODO: pop manual fee
			Close();
			return;
		}

		FeeChart.UpdateFeeEstimates(feeEstimates, _transactionInfo.MaximumPossibleFeeRate);

		if (_transactionInfo.FeeRate != FeeRate.Zero)
		{
			FeeChart.InitCurrentConfirmationTarget(_transactionInfo.FeeRate);
		}

		if (_isSilent)
		{
			_transactionInfo.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(FeeChart.CurrentConfirmationTarget);

			Complete();
		}
		else
		{
			IsBusy = false;
		}
	}
}
