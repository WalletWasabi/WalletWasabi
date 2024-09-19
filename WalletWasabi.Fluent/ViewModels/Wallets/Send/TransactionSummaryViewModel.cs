using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly IWalletModel _wallet;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _isCustomFeeUsed;
	[AutoNotify] private bool _isOtherPocketSelectionPossible;
	[AutoNotify] private LabelsArray _labels = LabelsArray.Empty;
	[AutoNotify] private LabelsArray _recipient = LabelsArray.Empty;
	[AutoNotify] private Amount? _fee;
	[AutoNotify] private Amount? _amount;
	[AutoNotify] private FeeRate? _feeRate;
	[AutoNotify] private double? _amountDiff;
	[AutoNotify] private double? _feeDiff;

	private TransactionSummaryViewModel(TransactionPreviewViewModel parent, IWalletModel wallet, TransactionInfo info, bool isPreview = false)
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

		ConfirmationTime = _wallet.Transactions.TryEstimateConfirmationTime(info);

		var destinationAmount = _transaction.CalculateDestinationAmount(info.Destination);

		Amount = UiContext.AmountProvider.Create(destinationAmount);
		Fee = UiContext.AmountProvider.Create(_transaction.Fee);
		FeeRate = info.FeeRate;

		Recipient = info.Recipient;
		IsCustomFeeUsed = info.IsCustomFeeUsed;
		IsOtherPocketSelectionPossible = info.IsOtherPocketSelectionPossible;
		AmountDiff = DiffOrNull(Amount, Parent.CurrentTransactionSummary.Amount);
		FeeDiff = DiffOrNull(Fee, Parent.CurrentTransactionSummary.Fee);
	}

	private static double? DiffOrNull(Amount? current, Amount? previous)
	{
		if (current is null || previous is null)
		{
			return null;
		}

		return current.Diff(previous);
	}
}
