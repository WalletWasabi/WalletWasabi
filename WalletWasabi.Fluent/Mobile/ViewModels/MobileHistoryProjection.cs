using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>
/// Presentation of individual transactions, not synthetic CoinJoin or CPFP grouping rows.
/// Monetary arithmetic remains in integer satoshis; doubles are used only by the renderer.
/// </summary>
public static class MobileHistoryProjection
{
	public static TransactionModel[] Flatten(IEnumerable<TransactionModel> roots)
	{
		ArgumentNullException.ThrowIfNull(roots);
		var pending = new Stack<TransactionModel>(roots.Reverse());
		var visited = new HashSet<TransactionModel>(ReferenceEqualityComparer.Instance);
		var identities = new HashSet<uint256>();
		var transactions = new List<TransactionModel>();
		while (pending.TryPop(out var node))
		{
			if (node is null || !visited.Add(node)) continue;
			if (node.Children.Count > 0)
			{
				// TransactionTreeBuilder puts the original parent AND the fee-bump
				// transactions in a CPFP group's children. Including the wrapper as
				// another transaction would double-count the parent's balance change.
				for (var index = node.Children.Count - 1; index >= 0; index--) pending.Push(node.Children[index]);
			}
			else if (!node.IsCoinjoinGroup && identities.Add(node.Id)) transactions.Add(node);
		}
		return transactions.OrderByDescending(x => x.Date).ThenByDescending(x => x.OrderIndex).ToArray();
	}

	public static string Title(TransactionType type) => type switch
	{
		TransactionType.IncomingTransaction => "Received",
		TransactionType.OutgoingTransaction => "Sent",
		TransactionType.SelfTransferTransaction => "Self transfer",
		TransactionType.Coinjoin or TransactionType.CoinjoinGroup => "CoinJoin",
		TransactionType.Cancellation => "Cancellation",
		TransactionType.CPFP => "Fee boost",
		_ => "Transaction"
	};

	public static string Icon(TransactionType type) => type switch
	{
		TransactionType.IncomingTransaction => "receive",
		TransactionType.OutgoingTransaction => "send",
		TransactionType.SelfTransferTransaction => "coins",
		TransactionType.Coinjoin or TransactionType.CoinjoinGroup => "coinjoin",
		TransactionType.Cancellation => "close",
		TransactionType.CPFP => "send",
		_ => "history"
	};

	public static bool MatchesFilter(TransactionType type, string filter) => filter switch
	{
		"All" => true,
		"Received" => type == TransactionType.IncomingTransaction,
		"Sent" => type == TransactionType.OutgoingTransaction,
		"CoinJoin" => type is TransactionType.Coinjoin or TransactionType.CoinjoinGroup,
		_ => false
	};

	public static IReadOnlyList<double> BalanceHistory(long currentSatoshis,
		IEnumerable<TransactionModel> newestFirst, int maximumTransactions = 30)
	{
		ArgumentNullException.ThrowIfNull(newestFirst);
		if (maximumTransactions is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(maximumTransactions));
		if (currentSatoshis < 0) return Array.Empty<double>();
		var balances = new List<long>(maximumTransactions + 1) { currentSatoshis };
		var balance = currentSatoshis;
		try
		{
			foreach (var transaction in newestFirst.Take(maximumTransactions))
			{
				balance = checked(balance - transaction.Amount.Satoshi);
				// Balances and history may arrive as separate live snapshots. Hide
				// inconsistent data instead of drawing a fabricated/clamped balance.
				if (balance < 0) return Array.Empty<double>();
				balances.Add(balance);
			}
		}
		catch (OverflowException) { return Array.Empty<double>(); }
		balances.Reverse();
		return balances.Select(value => value / 100_000_000d).ToArray();
	}
}
