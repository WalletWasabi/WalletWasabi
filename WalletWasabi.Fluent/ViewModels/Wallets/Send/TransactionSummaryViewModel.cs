using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private readonly BitcoinAddress _address;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private string _amountText = "";
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private bool _transactionHasPockets;
	[AutoNotify] private string _confirmationTimeText = "";
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _maxPrivacy;
	[AutoNotify] private bool _isCustomFeeUsed;
	[AutoNotify] private bool _isOtherPocketSelectionPossible;
	[AutoNotify] private SmartLabel _labels = SmartLabel.Empty;

	public TransactionSummaryViewModel(TransactionPreviewViewModel parent, Wallet wallet, TransactionInfo info, BitcoinAddress address, bool isPreview = false)
	{
		Parent = parent;
		_wallet = wallet;
		_address = address;
		IsPreview = isPreview;

		this.WhenAnyValue(x => x.TransactionHasChange, x => x.TransactionHasPockets)
			.Subscribe(_ => MaxPrivacy = !TransactionHasPockets && !TransactionHasChange);

		AddressText = _address.ToString();
		PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
		IsPayJoin = PayJoinUrl is not null;
	}

	public TransactionPreviewViewModel Parent { get; }

	public bool IsPreview { get; }

	public string AddressText { get; }

	public string? PayJoinUrl { get; }

	public bool IsPayJoin { get; }

	public void UpdateTransaction(BuildTransactionResult transactionResult, TransactionInfo info)
	{
		_transaction = transactionResult;

		ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(info.ConfirmationTimeSpan)} ";

		var destinationAmount = _transaction.CalculateDestinationAmount();
		var btcAmountText = $"{destinationAmount.ToFormattedString()} BTC";
		var fiatAmountText = destinationAmount.ToDecimal(MoneyUnit.BTC).GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
		AmountText = $"{btcAmountText} {fiatAmountText}";

		var fee = _transaction.Fee;
		var feeText = fee.ToFeeDisplayUnitString();
		var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC).GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
		FeeText = $"{feeText} {fiatFeeText}";

		TransactionHasChange =
			_transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != _address.ScriptPubKey);

		TransactionHasPockets = !info.IsPrivate;

		Labels = SmartLabel.Merge(transactionResult.SpentCoins.Select(x => x.GetLabels(info.PrivateCoinThreshold)));
		var exactPocketUsed = Labels.Count() == info.UserLabels.Count() && Labels.All(label => info.UserLabels.Contains(label, StringComparer.OrdinalIgnoreCase));
		TransactionHasPockets = Labels.Any() && !exactPocketUsed;

		IsCustomFeeUsed = info.IsCustomFeeUsed;
		IsOtherPocketSelectionPossible = info.IsOtherPocketSelectionPossible;
	}
}
