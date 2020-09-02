using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionFeeInfo : ReactiveObject
	{
		private decimal _feePercentage;
		private decimal _usdExchangeRate;
		private decimal _usdFee;
		private FeeRate? _feeRate;
		private Money? _estimatedBtcFee;

		public TransactionFeeInfo()
		{
			FeePercentage = 0;
			UsdExchangeRate = 0;
			UsdFee = 0;
			FeeRate = FeeRate.Zero;
			EstimatedBtcFee = Money.Zero;
		}

		public TransactionFeeInfo(decimal feePercentage, decimal usdExchangeRate, decimal usdFee, FeeRate feeRate, Money estimatedBtcFee)
		{
			FeePercentage = feePercentage;
			UsdExchangeRate = usdExchangeRate;
			UsdFee = usdFee;
			FeeRate = feeRate;
			EstimatedBtcFee = estimatedBtcFee;
		}

		public decimal FeePercentage
		{
			get => _feePercentage;
			set => this.RaiseAndSetIfChanged(ref _feePercentage, value);
		}

		public decimal UsdExchangeRate
		{
			get => _usdExchangeRate;
			set => this.RaiseAndSetIfChanged(ref _usdExchangeRate, value);
		}

		public decimal UsdFee
		{
			get => _usdFee;
			set => this.RaiseAndSetIfChanged(ref _usdFee, value);
		}

		public FeeRate FeeRate
		{
			get => _feeRate;
			set => this.RaiseAndSetIfChanged(ref _feeRate, value);
		}

		public Money EstimatedBtcFee
		{
			get => _estimatedBtcFee;
			set => this.RaiseAndSetIfChanged(ref _estimatedBtcFee, value);
		}
	}
}
