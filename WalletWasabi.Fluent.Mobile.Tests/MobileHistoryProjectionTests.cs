using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileHistoryProjectionTests
{
	[Fact]
	public void CpfpGroupIncludesOriginalParentAndFeeBoostButNotTheWrapper()
	{
		var original = Transaction(1, TransactionType.OutgoingTransaction, -100_500);
		var boost = Transaction(2, TransactionType.CPFP, -300);
		var group = Transaction(1, TransactionType.OutgoingTransaction, -100_500);
		group.Add(original); group.Add(boost);
		var rows = MobileHistoryProjection.Flatten(new[] { group });
		Assert.Equal(2, rows.Length);
		Assert.Contains(original, rows);
		Assert.Contains(boost, rows);
		Assert.DoesNotContain(group, rows);
		Assert.Equal(-100_800, rows.Sum(x => x.Amount.Satoshi));
	}

	[Fact]
	public void NestedGroupsAndDuplicateReferencesYieldEachActualTransactionOnce()
	{
		var coinjoin = Transaction(3, TransactionType.Coinjoin, -100);
		var otherCoinjoin = Transaction(4, TransactionType.Coinjoin, -120);
		var group = Transaction(3, TransactionType.CoinjoinGroup, -220);
		group.Add(coinjoin); group.Add(otherCoinjoin);
		var wrapper = Transaction(3, TransactionType.CoinjoinGroup, -220);
		wrapper.Add(group); wrapper.Add(coinjoin);
		var rows = MobileHistoryProjection.Flatten(new[] { wrapper, group, coinjoin });
		Assert.Equal(2, rows.Length);
		Assert.Same(otherCoinjoin, rows[0]);
		Assert.Same(coinjoin, rows[1]);
	}

	[Fact]
	public void EmptyCoinJoinGroupIsNotAPaymentAndCyclesCannotOverflowTheStack()
	{
		var empty = Transaction(1, TransactionType.CoinjoinGroup, 0);
		Assert.Empty(MobileHistoryProjection.Flatten(new[] { empty }));
		var leaf = Transaction(2, TransactionType.IncomingTransaction, 100);
		empty.Add(empty); empty.Add(leaf);
		Assert.Same(leaf, Assert.Single(MobileHistoryProjection.Flatten(new[] { empty })));
	}

	[Theory]
	[InlineData(TransactionType.IncomingTransaction, "Received", "receive")]
	[InlineData(TransactionType.OutgoingTransaction, "Sent", "send")]
	[InlineData(TransactionType.SelfTransferTransaction, "Self transfer", "coins")]
	[InlineData(TransactionType.Coinjoin, "CoinJoin", "coinjoin")]
	[InlineData(TransactionType.CoinjoinGroup, "CoinJoin", "coinjoin")]
	[InlineData(TransactionType.Cancellation, "Cancellation", "close")]
	[InlineData(TransactionType.CPFP, "Fee boost", "send")]
	[InlineData(TransactionType.Unknown, "Transaction", "history")]
	public void TransactionTypeDeterminesTheLabelNotTheAmountSign(TransactionType type, string title, string icon)
	{
		Assert.Equal(title, MobileHistoryProjection.Title(type));
		Assert.Equal(icon, MobileHistoryProjection.Icon(type));
	}

	[Theory]
	[InlineData(TransactionType.SelfTransferTransaction)]
	[InlineData(TransactionType.Cancellation)]
	[InlineData(TransactionType.CPFP)]
	[InlineData(TransactionType.Unknown)]
	public void NonPaymentTransactionsRemainInAllButNotSentOrReceived(TransactionType type)
	{
		Assert.True(MobileHistoryProjection.MatchesFilter(type, "All"));
		Assert.False(MobileHistoryProjection.MatchesFilter(type, "Sent"));
		Assert.False(MobileHistoryProjection.MatchesFilter(type, "Received"));
	}

	[Fact]
	public void BalanceReconstructionUsesExactSatoshisBeforeRenderingConversion()
	{
		var rows = new[] { Transaction(3, TransactionType.CPFP, -1), Transaction(2, TransactionType.OutgoingTransaction, -2), Transaction(1, TransactionType.IncomingTransaction, 10) };
		var balances = MobileHistoryProjection.BalanceHistory(7, rows);
		Assert.Equal(new[] { 0d, 0.00000010d, 0.00000008d, 0.00000007d }, balances);
	}

	[Fact]
	public void InconsistentSnapshotDoesNotFabricateAClampedChart()
	{
		Assert.Empty(MobileHistoryProjection.BalanceHistory(5, new[] { Transaction(1, TransactionType.IncomingTransaction, 10) }));
		Assert.Empty(MobileHistoryProjection.BalanceHistory(-1, Array.Empty<TransactionModel>()));
	}

	[Fact]
	public void HistoryWindowRemainsBoundedAndKeepsTheCurrentBalance()
	{
		var rows = Enumerable.Range(1, 100).Select(x => Transaction((ulong)x, TransactionType.CPFP, -1));
		var balances = MobileHistoryProjection.BalanceHistory(7, rows, maximumTransactions: 3);
		Assert.Equal(new[] { 0.00000010d, 0.00000009d, 0.00000008d, 0.00000007d }, balances);
	}

	[Fact]
	public void ExistingRowNotifiesWhenItsSemanticTransactionTypeChanges()
	{
		var unknown = Transaction(1, TransactionType.Unknown, -300);
		var self = Transaction(1, TransactionType.SelfTransferTransaction, -300);
		var row = new MobileTransactionItem(unknown, false, 0, _ => { });
		var changed = new List<string?>();
		row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		row.Update(self, false, 0);
		Assert.Equal("Self transfer", row.Title);
		Assert.Contains(nameof(row.Title), changed);
		Assert.False(row.IsIncoming);
	}

	private static TransactionModel Transaction(ulong id, TransactionType type, long satoshis) => new()
	{
		Id = new uint256(id), OrderIndex = (int)id, Type = type, Amount = Money.Satoshis(satoshis),
		Date = DateTimeOffset.UnixEpoch.AddMinutes((double)id), DateString = "Synthetic date", DateToolTipString = "Synthetic date",
		Labels = new LabelsArray(new[] { "Synthetic fixture" }), Confirmations = 0, ConfirmedTooltip = "Pending", Status = TransactionStatus.Pending,
		HexFunction = () => string.Empty,
		ForeignInputsFunction = () => Array.Empty<OutPoint>(), ForeignOutputsFunction = () => Array.Empty<IndexedTxOut>(),
		WalletInputs = Array.Empty<SmartCoin>(), WalletOutputs = Array.Empty<SmartCoin>()
	};
}
