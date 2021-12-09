using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _info;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private SmartLabel _labels;
	[AutoNotify] private string _amountText = "";
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private bool _transactionHasPockets;
	[AutoNotify] private string _confirmationTimeText = "";
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _maxPrivacy;
	[AutoNotify] private bool _isCustomFeeUsed;

	public TransactionSummaryViewModel(Wallet wallet, TransactionInfo info)
	{
		_wallet = wallet;
		_info = info;

		_labels = SmartLabel.Empty;

		this.WhenAnyValue(x => x.TransactionHasChange, x => x.TransactionHasPockets)
			.Subscribe(_ => { MaxPrivacy = !TransactionHasPockets && !TransactionHasChange; });

		AddressText = info.Address.ToString();
		PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
		IsPayJoin = PayJoinUrl is not null;
	}

	public string AddressText { get; }

	public string? PayJoinUrl { get; }

	public bool IsPayJoin { get; }

	public void UpdateTransaction(BuildTransactionResult transactionResult)
	{
		_transaction = transactionResult;

		ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(_info.ConfirmationTimeSpan)} ";

		var destinationAmount = _transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
		var btcAmountText = $"{destinationAmount} bitcoins ";
		var fiatAmountText =
			destinationAmount.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
		AmountText = $"{btcAmountText}{fiatAmountText}";

		var fee = _transaction.Fee;
		var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} sats ";
		var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC)
			.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");

		Labels = SmartLabel.Merge(_info.UserLabels,
			SmartLabel.Merge(transactionResult.SpentCoins.Select(x => CoinHelpers.GetLabels(x))));

		FeeText = $"{btcFeeText}{fiatFeeText}";

		TransactionHasChange =
			_transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != _info.Address.ScriptPubKey);

		TransactionHasPockets = !_info.IsPrivatePocketUsed;

		IsCustomFeeUsed = _info.IsCustomFeeUsed;
	}
}
