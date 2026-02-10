using DynamicData;
using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class TransactionTreeBuilder
{
	private readonly Wallet _wallet;

	public TransactionTreeBuilder(Wallet wallet)
	{
		_wallet = wallet;
	}

	public async Task<IEnumerable<TransactionModel>> BuildAsync(List<TransactionSummary> summaries, CancellationToken cancellationToken)
	{
		TransactionModel? coinJoinGroup = default;

		var result = new List<TransactionModel>();

		for (var i = 0; i < summaries.Count; i++)
		{
			var item = summaries[i];

			if (!item.IsOwnCoinjoin())
			{
				result.Add(await CreateRegularAsync(i, item, cancellationToken));
			}

			if (item.IsOwnCoinjoin())
			{
				coinJoinGroup ??= await CreateCoinjoinGroupAsync(i, item, cancellationToken);

				coinJoinGroup.Add(await CreateCoinjoinTransactionAsync(i, item, cancellationToken));
			}

			if (coinJoinGroup is { } cjg &&
				((i + 1 < summaries.Count && !summaries[i + 1].IsOwnCoinjoin()) || // The next item is not CJ so add the group.
				 i == summaries.Count - 1)) // There is no following item in the list so add the group.
			{
				if (cjg.Children.Count == 1)
				{
					result.Add(cjg.Children[0]);
				}
				else
				{
					UpdateCoinjoinGroup(cjg);
					result.Add(cjg);
				}

				coinJoinGroup = null;
			}
		}

		// This second iteration is necessary to transform the flat list of speed-ups into actual groups.
		// Here are the steps:
		// 1. Identify which transactions are CPFP (parents) and their children.
		// 2. Create a speed-up group with parent and children.
		// 3. Remove the previously added items from the history (they should no longer be there, but in the group).
		// 4. Add the group.
		foreach (var summary in summaries)
		{
			if (summary.Transaction.IsCPFPd)
			{
				// Group creation.
				var childrenTxs = summary.Transaction.ChildrenPayForThisTx;

				if (!TryFindHistoryItem(summary.GetHash(), result, out var parent))
				{
					continue; // If the parent transaction is not found, continue with the next summary.
				}

				var groupItems = new List<TransactionModel> { parent };
				foreach (var childTx in childrenTxs)
				{
					if (TryFindHistoryItem(childTx.GetHash(), result, out var child))
					{
						groupItems.Add(child);
					}
				}

				// If there is only one item in the group, it's not a group.
				// This can happen, for example, when CPFP occurs between user-owned wallets.
				if (groupItems.Count <= 1)
				{
					continue;
				}

				var speedUpGroup = CreateSpeedUpGroup(summary, parent, groupItems);
				result.Add(speedUpGroup);

				// Remove the items.
				result.RemoveMany(groupItems);
			}
		}

		return result;
	}

	private bool TryFindHistoryItem(uint256 txid, IEnumerable<TransactionModel> history, [NotNullWhen(true)] out TransactionModel? found)
	{
		found = history.SingleOrDefault(x => x.Id == txid);
		return found is not null;
	}

	private async Task<TransactionModel> CreateRegularAsync(int index, TransactionSummary transactionSummary, CancellationToken cancellationToken)
	{
		var itemType = GetItemType(transactionSummary);
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var confirmations = transactionSummary.GetConfirmations();
		var status = GetItemStatus(transactionSummary);
		var haveFeeEstimations = _wallet.FeeRateEstimations is not null;

		return new TransactionModel
		{
			Id = transactionSummary.GetHash(),
			Amount = transactionSummary.Amount,
			HasBeenSpedUp = transactionSummary.IsSpeedup,
			OrderIndex = index,
			Labels = transactionSummary.Labels,
			Date = date,
			DateString = date.ToUserFacingFriendlyString(),
			DateToolTipString = date.ToUserFacingString(),
			CanCancelTransaction = transactionSummary.Transaction.IsCancellable(_wallet.KeyManager) && haveFeeEstimations,
			CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(_wallet.KeyManager) && haveFeeEstimations,
			Type = itemType,
			Status = status,
			Confirmations = confirmations,
			BlockHeight = transactionSummary.Height is Height.ChainHeight(var h) ? h : 0u, // FIXME: this is wrong. Only confirmed txs have a BlockHeigh
			BlockHash = transactionSummary.BlockHash,
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			Fee = transactionSummary.GetFee(),
			FeeRate = transactionSummary.FeeRate(),
			ConfirmedTooltip = await GetConfirmationToolTipAsync(status, confirmations, transactionSummary.Transaction, cancellationToken),
		};
	}

	private async Task<TransactionModel> CreateCoinjoinGroupAsync(int index, TransactionSummary transactionSummary, CancellationToken cancellationToken)
	{
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var confirmations = transactionSummary.GetConfirmations();
		var status = GetItemStatus(transactionSummary);

		return new TransactionModel
		{
			Amount = Money.Zero,
			Labels = transactionSummary.Labels,
			Confirmations = confirmations,
			ConfirmedTooltip = await GetConfirmationToolTipAsync(status, confirmations, transactionSummary.Transaction, cancellationToken),
			Id = transactionSummary.GetHash(),
			Date = date,
			DateString = date.ToUserFacingFriendlyString(),
			DateToolTipString = date.ToUserFacingString(),
			OrderIndex = index,
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			Type = TransactionType.CoinjoinGroup,
			Status = status,
		};
	}

	private TransactionModel CreateSpeedUpGroup(TransactionSummary transactionSummary, TransactionModel parent, IEnumerable<TransactionModel> children)
	{
		var isConfirmed = children.All(x => x.IsConfirmed);

		var result = new TransactionModel
		{
			Id = transactionSummary.GetHash(),
			Amount = parent.Amount,
			OrderIndex = parent.OrderIndex,
			Date = parent.Date.ToLocalTime(),
			DateString = parent.DateString,
			DateToolTipString = parent.DateToolTipString,
			Confirmations = parent.Confirmations,
			BlockHeight = parent.BlockHeight,
			BlockHash = parent.BlockHash,
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			ConfirmedTooltip = parent.ConfirmedTooltip,
			Labels = parent.Labels,
			CanCancelTransaction = transactionSummary.Transaction.IsCancellable(_wallet.KeyManager),
			CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(_wallet.KeyManager),

			Type = GetItemType(transactionSummary),
			Status =
				isConfirmed
				? TransactionStatus.Confirmed
				: TransactionStatus.Pending,
			HasBeenSpedUp = true,
		};

		var dates = children.Select(tx => tx.Date).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();
		if (firstDate.Day == lastDate.Day)
		{
			foreach (var child in children)
			{
				child.DateString = child.Date.ToLocalTime().ToOnlyTimeString();
			}
		}

		foreach (var child in children)
		{
			result.Add(child);
			child.IsChild = true;
		}

		return result;
	}

	private void UpdateCoinjoinGroup(TransactionModel coinjoinGroup)
	{
		foreach (var child in coinjoinGroup.Children)
		{
			child.IsChild = true;
		}

		var isConfirmed = coinjoinGroup.Children.All(x => x.IsConfirmed);
		coinjoinGroup.Status =
			isConfirmed
			? TransactionStatus.Confirmed
			: TransactionStatus.Pending;

		coinjoinGroup.ConfirmedTooltip = coinjoinGroup.Children.MinBy(x => x.Confirmations)?.ConfirmedTooltip ?? "";
		coinjoinGroup.Date = coinjoinGroup.Children.Select(tx => tx.Date).Max().ToLocalTime();

		var amount = coinjoinGroup.Children.Sum(x => x.Amount);
		coinjoinGroup.Amount = amount;

		var fee = coinjoinGroup.Children.Sum(x => x.Fee ?? Money.Zero);
		coinjoinGroup.Fee = fee;

		var dates = coinjoinGroup.Children.Select(tx => tx.Date).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();

		coinjoinGroup.DateString = lastDate.ToUserFacingFriendlyString();

		if (firstDate.Day == lastDate.Day)
		{
			coinjoinGroup.DateToolTipString = $"{firstDate.ToUserFacingString(withTime: false)}";

			foreach (var child in coinjoinGroup.Children)
			{
				child.DateString = child.Date.ToLocalTime().ToOnlyTimeString();
			}
		}
		else
		{
			coinjoinGroup.DateToolTipString = $"{firstDate.ToUserFacingString(withTime: true)} - {lastDate.ToUserFacingString(withTime: true)}";
		}
	}

	private async Task<TransactionModel> CreateCoinjoinTransactionAsync(int index, TransactionSummary transactionSummary, CancellationToken cancellationToken)
	{
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var confirmations = transactionSummary.GetConfirmations();
		var status = GetItemStatus(transactionSummary);

		return new TransactionModel
		{
			Id = transactionSummary.GetHash(),
			Amount = transactionSummary.Amount,
			OrderIndex = index,
			Date = date,
			DateString = date.ToUserFacingFriendlyString(),
			DateToolTipString = date.ToUserFacingString(),
			Labels = transactionSummary.Labels,
			Type = TransactionType.Coinjoin,
			Status = status,
			Confirmations = confirmations,
			BlockHeight = transactionSummary.Height is Height.ChainHeight(var h) ? h : 0,
			BlockHash = transactionSummary.BlockHash,
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			ConfirmedTooltip = await GetConfirmationToolTipAsync(status, confirmations, transactionSummary.Transaction, cancellationToken),
			Fee = transactionSummary.GetFee(),
			FeeRate = transactionSummary.FeeRate()
		};
	}

	private TransactionType GetItemType(TransactionSummary transactionSummary)
	{
		var isSelfSpend = transactionSummary.Amount == -(transactionSummary.GetFee() ?? Money.Zero);
		if (!transactionSummary.IsCancellation && !transactionSummary.IsCPFP && isSelfSpend)
		{
			return TransactionType.SelfTransferTransaction;
		}

		if (!transactionSummary.IsCPFP && transactionSummary.Amount > Money.Zero)
		{
			return TransactionType.IncomingTransaction;
		}

		if (!transactionSummary.IsCPFP && !transactionSummary.IsCancellation && transactionSummary.Amount < Money.Zero)
		{
			return TransactionType.OutgoingTransaction;
		}

		if (transactionSummary.IsCancellation)
		{
			return TransactionType.Cancellation;
		}

		if (transactionSummary.IsCPFP)
		{
			return TransactionType.CPFP;
		}

		return TransactionType.Unknown;
	}

	private static TransactionStatus GetItemStatus(TransactionSummary transactionSummary)
	{
		return transactionSummary.IsConfirmed() ? TransactionStatus.Confirmed : TransactionStatus.Pending;
	}

	private async Task<string> GetConfirmationToolTipAsync(TransactionStatus status, uint confirmations, SmartTransaction smartTransaction, CancellationToken cancellationToken)
	{
		if (status == TransactionStatus.Confirmed)
		{
			return TextHelpers.GetConfirmationText(confirmations);
		}

		var friendlyString = await TransactionFeeHelper.EstimateConfirmationTimeAsync(_wallet.FeeRateEstimations, _wallet.Network, smartTransaction, _wallet.CpfpInfoProvider, cancellationToken) is { } estimate
			? TextHelpers.TimeSpanToFriendlyString(estimate)
			: "";

		return (status, friendlyString != "") switch
		{
			(TransactionStatus.Pending, true) => $"Pending (confirming in ≈ {friendlyString})",
			(TransactionStatus.Pending, false) => "Pending",
			_ => "Unknown"
		};
	}
}
