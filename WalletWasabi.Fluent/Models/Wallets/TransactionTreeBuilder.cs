using DynamicData;
using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

public class TransactionTreeBuilder
{
	private readonly KeyManager _keyManager;

	public TransactionTreeBuilder(KeyManager keyManager)
	{
		_keyManager = keyManager;
	}

	public IEnumerable<TransactionModel> Build(List<TransactionSummary> summaries)
	{
		Money balance = Money.Zero;
		TransactionModel? coinJoinGroup = default;

		var result = new List<TransactionModel>();

		for (var i = 0; i < summaries.Count; i++)
		{
			var item = summaries[i];

			balance += item.Amount;

			if (!item.IsOwnCoinjoin())
			{
				result.Add(CreateRegular(i, item, balance));
			}

			if (item.IsOwnCoinjoin())
			{
				coinJoinGroup ??= CreateCoinjoinGroup(i, item);

				coinJoinGroup.Add(CreateCoinjoinTransaction(i, item, balance));
			}

			if (coinJoinGroup is { } cjg &&
				((i + 1 < summaries.Count && !summaries[i + 1].IsOwnCoinjoin()) || // The next item is not CJ so add the group.
				 i == summaries.Count - 1)) // There is no following item in the list so add the group.
			{
				if (cjg.Children.Count == 1)
				{
					var singleCjItem = CreateCoinjoinTransaction(cjg.OrderIndex, cjg.Children[0].TransactionSummary, balance);
					result.Add(singleCjItem);
				}
				else
				{
					UpdateCoinjoinGroup(cjg, balance);
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

				var speedUpGroup = CreateSpeedUp(summary, parent, groupItems);

				// Check if the last item's balance is not null before calling SetBalance.
				var bal = groupItems.Last().Balance;
				if (bal is not null)
				{
					speedUpGroup.Balance = bal;
				}
				else
				{
					continue;
				}

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

	private TransactionModel CreateRegular(int index, TransactionSummary transactionSummary, Money balance)
	{
		var amounts = GetAmounts(transactionSummary);
		var itemType = TransactionType.Unknown;

		if (!transactionSummary.IsCPFP && amounts.IncomingAmount is { } incomingAmount && incomingAmount > Money.Zero)
		{
			itemType = TransactionType.IncomingTransaction;
		}

		if (!transactionSummary.IsCPFP && amounts.OutgoingAmount is { } outgoingAmount && outgoingAmount > Money.Zero)
		{
			itemType = TransactionType.OutgoingTransaction;
		}

		if (transactionSummary.IsCancellation)
		{
			itemType = TransactionType.Cancellation;
		}

		if (transactionSummary.IsCPFP)
		{
			itemType = TransactionType.CPFP;
		}

		if (amounts.OutgoingAmount == Money.Zero)
		{
			itemType = TransactionType.SelfTransferTransaction;
		}

		var date = transactionSummary.FirstSeen.ToLocalTime();
		var confirmations = transactionSummary.GetConfirmations();

		return new TransactionModel
		{
			TransactionSummary = transactionSummary,
			Id = transactionSummary.GetHash(),
			OrderIndex = index,
			Labels = transactionSummary.Labels,
			Date = date,
			DateString = date.ToUserFacingString(),
			Balance = balance,
			IncomingAmount = amounts.IncomingAmount,
			OutgoingAmount = amounts.OutgoingAmount,
			CanCancelTransaction = transactionSummary.Transaction.IsCancellable(_keyManager),
			CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(_keyManager),
			Type = itemType,
			Status = GetItemStatus(transactionSummary),
			Confirmations = confirmations,
			Fee = transactionSummary.GetFee(),
			ConfirmedTooltip = TextHelpers.GetConfirmationText(confirmations),
		};
	}

	private TransactionModel CreateCoinjoinGroup(int index, TransactionSummary transactionSummary)
	{
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var confirmations = transactionSummary.GetConfirmations();

		return new TransactionModel
		{
			Labels = transactionSummary.Labels,
			Confirmations = confirmations,
			ConfirmedTooltip = TextHelpers.GetConfirmationText(confirmations),
			TransactionSummary = transactionSummary,
			Id = transactionSummary.GetHash(),
			Date = date,
			DateString = date.ToUserFacingString(),
			OrderIndex = index,
			Type = TransactionType.CoinjoinGroup,
			Status = GetItemStatus(transactionSummary),
		};
	}

	private TransactionModel CreateSpeedUp(TransactionSummary transactionSummary, TransactionModel parent, IEnumerable<TransactionModel> children)
	{
		children = children.Reverse();

		var isConfirmed = children.All(x => x.IsConfirmed);

		var result = new TransactionModel
		{
			Id = transactionSummary.GetHash(),
			TransactionSummary = transactionSummary,
			OrderIndex = parent.OrderIndex,
			Date = parent.Date.ToLocalTime(),
			DateString = parent.DateString,
			Confirmations = parent.Confirmations,
			ConfirmedTooltip = parent.ConfirmedTooltip,
			Labels = parent.Labels,
			IncomingAmount = parent.IncomingAmount,
			OutgoingAmount = parent.OutgoingAmount,
			CanCancelTransaction = transactionSummary.Transaction.IsCancellable(_keyManager),
			CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(_keyManager),

			Type = TransactionType.CPFP,
			Status =
				isConfirmed
				? TransactionStatus.Confirmed
				: TransactionStatus.SpeedUp,
		};

		foreach (var child in children)
		{
			result.Add(child);
			child.IsChild = true;
		}

		return result;
	}

	private void UpdateCoinjoinGroup(TransactionModel coinjoinGroup, Money balance)
	{
		coinjoinGroup.Balance = balance;

		foreach (var child in coinjoinGroup.Children)
		{
			child.Balance = balance;
			balance -= child.Amount;

			child.IsChild = true;
		}

		var isConfirmed = coinjoinGroup.Children.All(x => x.IsConfirmed);
		coinjoinGroup.Status =
			isConfirmed
			? TransactionStatus.Confirmed
			: TransactionStatus.Pending;

		var confirmations = coinjoinGroup.Children.Select(x => x.Confirmations).Min();

		coinjoinGroup.ConfirmedTooltip = TextHelpers.GetConfirmationText(confirmations);
		coinjoinGroup.Date = coinjoinGroup.Children.Select(tx => tx.Date).Max().ToLocalTime();

		var amount = coinjoinGroup.Children.Sum(x => x.Amount);
		var fee = coinjoinGroup.Children.Sum(x => x.Fee ?? Money.Zero);

		var amounts = GetAmounts(amount, fee);

		coinjoinGroup.IncomingAmount = amounts.IncomingAmount;
		coinjoinGroup.OutgoingAmount = amounts.OutgoingAmount;

		var dates = coinjoinGroup.Children.Select(tx => tx.Date).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();

		coinjoinGroup.DateString =
			firstDate.Day == lastDate.Day
			? $"{firstDate.ToUserFacingString(withTime: false)}"
			: $"{firstDate.ToUserFacingString(withTime: false)} - {lastDate.ToUserFacingString(withTime: false)}";
	}

	private TransactionModel CreateCoinjoinTransaction(int index, TransactionSummary transactionSummary, Money balance)
	{
		var amounts = GetAmounts(transactionSummary);
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var confirmations = transactionSummary.GetConfirmations();

		return new TransactionModel
		{
			Id = transactionSummary.GetHash(),
			TransactionSummary = transactionSummary,
			OrderIndex = index,
			Date = date,
			DateString = date.ToUserFacingString(),
			Balance = balance,
			Labels = transactionSummary.Labels,

			IncomingAmount = amounts.IncomingAmount,
			OutgoingAmount = amounts.OutgoingAmount,

			Type = TransactionType.Coinjoin,
			Status = GetItemStatus(transactionSummary),
			Confirmations = confirmations,
			ConfirmedTooltip = TextHelpers.GetConfirmationText(confirmations),
			Fee = transactionSummary.GetFee()
		};
	}

	private TransactionStatus GetItemStatus(TransactionSummary transactionSummary)
	{
		var isConfirmed = transactionSummary.IsConfirmed();

		if (isConfirmed)
		{
			return TransactionStatus.Confirmed;
		}

		if (!isConfirmed && (transactionSummary.IsSpeedup || transactionSummary.IsCPFPd))
		{
			return TransactionStatus.SpeedUp;
		}

		if (!isConfirmed && !transactionSummary.IsSpeedup)
		{
			return TransactionStatus.Pending;
		}

		return TransactionStatus.Unknown;
	}

	private (Money? IncomingAmount, Money? OutgoingAmount) GetAmounts(TransactionSummary transactionSummary)
	{
		return GetAmounts(transactionSummary.Amount, transactionSummary.GetFee());
	}

	private (Money? IncomingAmount, Money? OutgoingAmount) GetAmounts(Money amount, Money? fee)
	{
		Money? incomingAmount = null;
		Money? outgoingAmount = null;

		if (amount < Money.Zero)
		{
			outgoingAmount = -amount - (fee ?? Money.Zero);
		}
		else
		{
			incomingAmount = amount;
		}

		return (incomingAmount, outgoingAmount);
	}
}
