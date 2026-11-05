using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class TransactionPreviewObservableTests
{
	[Fact]
	public void CoinjoinStatusChangesRequestWarningRefresh()
	{
		var transaction = CreateTransaction();
		var coin = transaction.SpentCoins.Single();
		using var transactions = new BehaviorSubject<BuildTransactionResult?>(transaction);
		var refreshes = new List<BuildTransactionResult>();
		using var subscription = transactions.ObserveInputChanges().Subscribe(refreshes.Add);

		coin.CoinJoinInProgress = true;
		Assert.True(transaction.SpendsCoinjoining);
		Assert.Equal(2, refreshes.Count);

		coin.CoinJoinInProgress = false;
		Assert.False(transaction.SpendsCoinjoining);
		Assert.Equal(3, refreshes.Count);
	}

	[Fact]
	public void ReplacingTransactionStopsObservingOldInputs()
	{
		var first = CreateTransaction();
		var second = CreateTransaction();
		using var transactions = new BehaviorSubject<BuildTransactionResult?>(first);
		var refreshes = new List<BuildTransactionResult>();
		using var subscription = transactions.ObserveInputChanges().Subscribe(refreshes.Add);

		transactions.OnNext(second);
		Assert.Equal(2, refreshes.Count);

		first.SpentCoins.Single().CoinJoinInProgress = true;
		Assert.Equal(2, refreshes.Count);

		second.SpentCoins.Single().CoinJoinInProgress = true;
		Assert.Equal(3, refreshes.Count);
		Assert.Same(second, refreshes.Last());
	}

	[Fact]
	public void ClearingTransactionStopsObservingInputs()
	{
		var transaction = CreateTransaction();
		using var transactions = new BehaviorSubject<BuildTransactionResult?>(transaction);
		var refreshes = new List<BuildTransactionResult>();
		using var subscription = transactions.ObserveInputChanges().Subscribe(refreshes.Add);

		transactions.OnNext(null);
		transaction.SpentCoins.Single().CoinJoinInProgress = true;

		Assert.Single(refreshes);
	}

	[Fact]
	public void DisposingSubscriptionStopsObservingInputs()
	{
		var transaction = CreateTransaction();
		using var transactions = new BehaviorSubject<BuildTransactionResult?>(transaction);
		var refreshes = new List<BuildTransactionResult>();
		var subscription = transactions.ObserveInputChanges().Subscribe(refreshes.Add);

		subscription.Dispose();
		transaction.SpentCoins.Single().CoinJoinInProgress = true;
		transactions.OnNext(CreateTransaction());

		Assert.Single(refreshes);
	}

	[Fact]
	public void AnonymityChangeRequestsWarningRefresh()
	{
		var transaction = CreateTransaction();
		var coin = transaction.SpentCoins.Single();
		using var transactions = new BehaviorSubject<BuildTransactionResult?>(transaction);
		var refreshes = new List<BuildTransactionResult>();
		using var subscription = transactions.ObserveInputChanges().Subscribe(refreshes.Add);

		var previousScore = coin.AnonymitySet;
		coin.SetAnonymitySet(previousScore + 1, transaction.Transaction.GetHash());
		Assert.Equal(previousScore + 1, coin.AnonymitySet);

		Assert.Equal(2, refreshes.Count);
		Assert.Same(transaction, refreshes.Last());
	}

	private static BuildTransactionResult CreateTransaction()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(ownInputCount: 1);
		return new BuildTransactionResult(
			transaction,
			PSBT.FromTransaction(transaction.Transaction, Network.Main),
			false, Money.Zero, 0m,
			new Dictionary<HdPubKey, LabelsArray>());
	}

	[Fact]
	public void ConfirmingInputRequestsWarningRefresh()
	{
		var smartTransaction = BitcoinFactory.CreateSmartTransaction(ownInputCount: 1);
		var coin = smartTransaction.WalletInputs.Single();
		coin.Height = Height.Mempool;

		var transaction = new BuildTransactionResult(
			smartTransaction,
			PSBT.FromTransaction(smartTransaction.Transaction, Network.Main),
			false, Money.Zero, 0m,
			new Dictionary<HdPubKey, LabelsArray>());

		using var transactions = new BehaviorSubject<BuildTransactionResult?>(transaction);
		var refreshes = new List<BuildTransactionResult>();
		using var subscription = transactions.ObserveInputChanges().Subscribe(refreshes.Add);

		Assert.Single(refreshes);
		Assert.True(transaction.SpendsUnconfirmed);

		coin.Height = new Height.ChainHeight(100);

		Assert.False(transaction.SpendsUnconfirmed);
		Assert.Equal(2, refreshes.Count);
		Assert.Same(transaction, refreshes[1]);
	}
}
