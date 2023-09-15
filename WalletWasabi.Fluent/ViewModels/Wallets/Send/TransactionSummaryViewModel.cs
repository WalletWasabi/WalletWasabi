using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private string _amountText = "";
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _isCustomFeeUsed;
	[AutoNotify] private bool _isOtherPocketSelectionPossible;
	[AutoNotify] private LabelsArray _labels = LabelsArray.Empty;
	[AutoNotify] private LabelsArray _recipient = LabelsArray.Empty;
	[AutoNotify] private string _fee = "";
	[AutoNotify] private string _amount = "";
	[AutoNotify] private BtcAmount _newAmount;

	public TransactionSummaryViewModel(TransactionPreviewViewModel parent, Wallet wallet, TransactionInfo info, bool isPreview = false)
	{
		Parent = parent;
		_wallet = wallet;
		IsPreview = isPreview;
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

		TransactionFeeHelper.TryEstimateConfirmationTime(_wallet, info.FeeRate, out var estimate);
		ConfirmationTime = estimate;

		var destinationAmount = _transaction.CalculateDestinationAmount(info.Destination);
		AmountText = $"{destinationAmount.ToFormattedString()} BTC";
		Amount = destinationAmount.ToString();

		NewAmount = new BtcAmount(destinationAmount, new ExchangeRateProvider(_wallet.Synchronizer));

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

		Recipient = info.Recipient;
		IsCustomFeeUsed = info.IsCustomFeeUsed;
		IsOtherPocketSelectionPossible = info.IsOtherPocketSelectionPossible;
	}
}
