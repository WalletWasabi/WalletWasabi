using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Services;
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

	private SendFeeViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
	{
		_isSilent = isSilent;
		IsBusy = isSilent;
		_wallet = wallet;
		_transactionInfo = transactionInfo;

		FeeChart = new FeeChartViewModel();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false, escapeGoesBack: true);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(OnNext);

		AdvancedOptionsCommand = ReactiveCommand.CreateFromTask(ShowAdvancedOptionsAsync);
	}

	public FeeChartViewModel FeeChart { get; }

	public ICommand AdvancedOptionsCommand { get; }

	private void OnNext()
	{
		var blockTarget = FeeChart.CurrentConfirmationTarget;
		_transactionInfo.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(blockTarget);
		Services.UiConfig.FeeTarget = (int)blockTarget;
		Close(DialogResultKind.Normal, new FeeRate(FeeChart.GetSatoshiPerByte(blockTarget)));
	}

	private async Task ShowAdvancedOptionsAsync()
	{
		var result = await ShowCustomFeeRateDialogAsync();
		if (result is { } feeRate && feeRate != FeeRate.Zero)
		{
			Close(DialogResultKind.Normal, feeRate);
		}
	}

	private async Task<FeeRate?> ShowCustomFeeRateDialogAsync()
	{
		var result = await NavigateDialogAsync(new CustomFeeRateDialogViewModel(_transactionInfo), NavigationTarget.CompactDialogScreen);
		return result.Result;
	}

	private async Task FeeEstimationsAreNotAvailableAsync()
	{
		await ShowErrorAsync(
			"Transaction fee",
			"Transaction fee estimations are not available at the moment. Try again later or you can enter the fee rate manually.",
			"",
			NavigationTarget.CompactDialogScreen);

		var customFeeRate = await ShowCustomFeeRateDialogAsync();

		if (customFeeRate is { })
		{
			Close(DialogResultKind.Normal, customFeeRate);
		}
		else
		{
			Close();
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		IsBusy = true;

		base.OnNavigatedTo(isInHistory, disposables);

		Services.EventBus.AsObservable<MiningFeeRatesChanged>()
			.Select(e =>
			{
				TransactionFeeHelper.TryGetFeeEstimates(e.AllFeeEstimate, _wallet.Network, out var estimates);
				return estimates;
			})
			.WhereNotNull()
			.Where(x => x.Estimations.Any())
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(estimations => FeeChart.UpdateFeeEstimates(estimations.WildEstimations, _transactionInfo.MaximumPossibleFeeRate))
			.DisposeWith(disposables);

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			FeeRateEstimations feeRateEstimations;
			using var cancelTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));

			try
			{
				feeRateEstimations = await TransactionFeeHelper.GetFeeEstimatesWhenReadyAsync(_wallet, cancelTokenSource.Token);
			}
			catch (Exception ex)
			{
				Logger.LogInfo(ex);
				await FeeEstimationsAreNotAvailableAsync();
				return;
			}

			FeeChart.UpdateFeeEstimates(feeRateEstimations.WildEstimations, _transactionInfo.MaximumPossibleFeeRate);

			if (_transactionInfo.FeeRate != FeeRate.Zero)
			{
				FeeChart.InitCurrentConfirmationTarget(_transactionInfo.FeeRate);
			}

			if (_isSilent)
			{
				_transactionInfo.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(FeeChart.CurrentConfirmationTarget);

				OnNext();
			}
			else
			{
				IsBusy = false;
			}
		});
	}
}
