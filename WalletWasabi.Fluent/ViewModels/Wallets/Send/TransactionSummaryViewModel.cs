using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;
using WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Outputs;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly IWalletModel _wallet;
	private readonly TransactionInfo _info;
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
	[AutoNotify] private InputsCoinListViewModel? _inputList;
	[AutoNotify] private OutputsCoinListViewModel? _outputList;
	[AutoNotify] private IReadOnlyList<RecipientSummaryViewModel> _recipients = Array.Empty<RecipientSummaryViewModel>();

	private TransactionSummaryViewModel(TransactionPreviewViewModel parent, IWalletModel wallet, TransactionInfo info, bool isPreview = false)
	{
		Parent = parent;
		_wallet = wallet;
		_info = info;
		IsPreview = isPreview;
		IsPayToMany = info.IsPayToMany;
		AddressText = info.Destination.ToString(_wallet.Network);
		PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
		IsPayJoin = PayJoinUrl is not null;
	}

	public TransactionPreviewViewModel Parent { get; }

	public bool IsPreview { get; }

	public string AddressText { get; }

	public string? PayJoinUrl { get; }

	public bool IsPayJoin { get; }

	public bool IsPayToMany { get; }

	public void UpdateTransaction(BuildTransactionResult transactionResult, TransactionInfo info)
	{
		_transaction = transactionResult;

		ConfirmationTime = _wallet.Transactions.TryEstimateConfirmationTime(info);

		Money destinationAmount;
		if (info.IsPayToMany)
		{
			var fee = _transaction.Fee;
			var hasSubtractFee = info.AllRecipients.Any(r => r.IsSubtractFee);

			// For pay-to-many, show per-recipient amounts. If a recipient used "Max" (SubtractFee),
			// display their actual received amount (requested minus fee) instead of the raw request.
			Recipients = info.AllRecipients.Select(r =>
			{
				var displayAmount = r.IsSubtractFee ? r.Amount - fee : r.Amount;
				return new RecipientSummaryViewModel(
					r.Destination.ToString(_wallet.Network),
					UiContext.AmountProvider.Create(displayAmount),
					r.Label);
			}).ToList();

			// Total destination amount: subtract fee only if one of the recipients absorbs it.
			destinationAmount = hasSubtractFee
				? info.TotalAmount - fee
				: info.TotalAmount;
		}
		else
		{
			destinationAmount = _transaction.CalculateDestinationAmount(info.Destination);
		}

		// Collect all destination scriptPubKeys (single or multiple recipients) so the outputs
		// list can distinguish actual destinations from change. Previously this was a single Script
		// matched against the primary destination only, which caused additional recipients' outputs
		// to be incorrectly labeled as "change".
		var destinationScripts = info.AllRecipients
			.Select(r => r.Destination.GetScriptPubKey())
			.ToHashSet();

		Amount = UiContext.AmountProvider.Create(destinationAmount);
		Fee = UiContext.AmountProvider.Create(_transaction.Fee);
		FeeRate = info.FeeRate;

		InputList = new InputsCoinListViewModel(
			transactionResult.Transaction.WalletInputs,
			_wallet.Network,
			transactionResult.Transaction.WalletInputs.Count + transactionResult.Transaction.ForeignInputs.Count,
			Parent.CurrentTransactionSummary.InputList?.TreeDataGridSource.Items.First().IsExpanded,
			!IsPreview ? null : Parent.CurrentTransactionSummary.InputList?.TreeDataGridSource.Items.First().Children.Count);

		OutputList = new OutputsCoinListViewModel(
			transactionResult.Transaction.WalletOutputs.Select(x => x.TxOut).ToList(),
			transactionResult.Transaction.ForeignOutputs.Select(x => x.TxOut).ToList(),
			_wallet.Network,
			destinationScripts,
			Parent.CurrentTransactionSummary.OutputList?.TreeDataGridSource.Items.First().IsExpanded,
			!IsPreview ? null : Parent.CurrentTransactionSummary.OutputList?.TreeDataGridSource.Items.First().Children.Count);

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
