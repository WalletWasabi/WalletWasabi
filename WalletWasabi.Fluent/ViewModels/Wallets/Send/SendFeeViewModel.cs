using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;
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
	private bool _isSliderChange;

	[AutoNotify] private string _feeRateString;

	public SendFeeViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
	{
		_isSilent = isSilent;
		IsBusy = isSilent;
		_wallet = wallet;
		_transactionInfo = transactionInfo;

		_feeRateString =
			transactionInfo.IsCustomFeeUsed
			? transactionInfo.FeeRate.SatoshiPerByte.ToString(CultureInfo.InvariantCulture)
			: "";

		FeeChart = new FeeChartViewModel();

		FeeChart.WhenAnyValue(x => x.CurrentSatoshiPerByte)
				.Subscribe(OnSliderValueChanged);

		this.WhenAnyValue(x => x.FeeRateString)
			.Subscribe(OnFeeRateStringChanged);

		this.ValidateProperty(x => x.FeeRateString, ValidateCustomFee);

		SetupCancel(false, true, false);
		EnableBack = true;

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.FeeRateString)
				.Select(_ =>
				{
					var noError = !Validations.Any;
					var somethingFilled = FeeRateString is not null or "";

					return noError && somethingFilled;
				});

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);
	}

	private void OnSliderValueChanged(decimal x)
	{
		if (_isSliderChange)
		{
			return;
		}

		_isSliderChange = true;

		_transactionInfo.IsCustomFeeUsed = false;
		_transactionInfo.FeeRate = FeeRate.Zero;
		FeeRateString = x.ToString("0");
		_isSliderChange = false;
	}

	public FeeChartViewModel FeeChart { get; }

	private void OnFeeRateStringChanged(string feeRateString)
	{
		if (_isSliderChange)
		{
			return;
		}

		if (decimal.TryParse(feeRateString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var feeRate))
		{
			_transactionInfo.FeeRate = new FeeRate(feeRate);
			_transactionInfo.IsCustomFeeUsed = true;
			_isSliderChange = true;
			FeeChart.InitCurrentConfirmationTarget(_transactionInfo.FeeRate);
			_isSliderChange = false;
		}
		else if (_transactionInfo.IsCustomFeeUsed)
		{
			_transactionInfo.FeeRate = FeeRate.Zero;
			_transactionInfo.IsCustomFeeUsed = false;
		}
	}

	private void ValidateCustomFee(IValidationErrors errors)
	{
		var customFeeString = FeeRateString;

		if (customFeeString is null or "")
		{
			return;
		}

		if (!decimal.TryParse(customFeeString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
		{
			errors.Add(ErrorSeverity.Error, "The entered fee is not valid.");
			return;
		}

		if (value < decimal.One)
		{
			errors.Add(ErrorSeverity.Error, "Cannot be less than 1 sat/vByte.");
			return;
		}

		try
		{
			_ = new FeeRate(value);
		}
		catch (OverflowException)
		{
			errors.Add(ErrorSeverity.Error, "The entered fee is too high.");
			return;
		}
	}

	private void OnNext()
	{
		if (_transactionInfo.IsCustomFeeUsed)
		{
			var feeRate = decimal.Parse(FeeRateString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
			Close(DialogResultKind.Normal, new FeeRate(feeRate));
		}
		else
		{
			_transactionInfo.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(FeeChart.CurrentConfirmationTarget);

			var blockTarget = FeeChart.CurrentConfirmationTarget;

			Services.UiConfig.FeeTarget = (int)blockTarget;
			Close(DialogResultKind.Normal, new FeeRate(FeeChart.GetSatoshiPerByte(blockTarget)));
		}
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
			.Subscribe(estimations => FeeChart.UpdateFeeEstimates(TransactionFeeHelper.GetFeeEstimates(_wallet), _transactionInfo.MaximumPossibleFeeRate))
			.DisposeWith(disposables);

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			while (feeProvider.AllFeeEstimate is null)
			{
				await Task.Delay(100);
			}

			FeeChart.UpdateFeeEstimates(TransactionFeeHelper.GetFeeEstimates(_wallet), _transactionInfo.MaximumPossibleFeeRate);

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
