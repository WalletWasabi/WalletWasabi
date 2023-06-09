using System.Linq;
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
	private BuildTransactionResult? _transaction;
	[AutoNotify] private string _amountText = "";
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private bool _transactionHasPockets;
	[AutoNotify] private string _confirmationTimeText = "";
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _maxPrivacy;
	[AutoNotify] private bool _isCustomFeeUsed;
	[AutoNotify] private bool _isOtherPocketSelectionPossible;
	[AutoNotify] private LabelsArray _labels = LabelsArray.Empty;
	[AutoNotify] private LabelsArray _recipient = LabelsArray.Empty;
	[AutoNotify] private string _fee = "";
	[AutoNotify] private string _amount = "";

	public TransactionSummaryViewModel(TransactionPreviewViewModel parent, Wallet wallet, TransactionInfo info, bool isPreview = false)
	{
		Parent = parent;
		_wallet = wallet;
		IsPreview = isPreview;

		this.WhenAnyValue(x => x.TransactionHasChange, x => x.TransactionHasPockets)
			.Subscribe(_ => MaxPrivacy = !TransactionHasPockets && !TransactionHasChange);

		AddressText = info.Destination.ToString();
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
		AmountText = $"{destinationAmount.ToFormattedString()} BTC";
		Amount = destinationAmount.ToString();

		var fee = _transaction.Fee;
		FeeText = fee.ToFeeDisplayUnitFormattedString();
		Fee = _transaction.Fee.ToFeeDisplayUnitRawString();

		var exchangeRate = _wallet.Synchronizer.UsdExchangeRate;
		if (exchangeRate != 0)
		{
			var fiatAmountText = destinationAmount.BtcToUsd(exchangeRate).ToUsdAproxBetweenParens();
			AmountText += $" {fiatAmountText}";

			var fiatFeeText = fee.BtcToUsd(exchangeRate).ToUsdAproxBetweenParens();
			FeeText += $" {fiatFeeText}";
		}

		TransactionHasChange =
			_transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != info.Destination.ScriptPubKey);

		Labels = new LabelsArray(transactionResult.SpentCoins.SelectMany(x => x.GetLabels(info.PrivateCoinThreshold)).Except(info.Recipient));
		TransactionHasPockets = Labels.Any();

		Recipient = info.Recipient;

		IsCustomFeeUsed = info.IsCustomFeeUsed;
		IsOtherPocketSelectionPossible = info.IsOtherPocketSelectionPossible;
	}
}
