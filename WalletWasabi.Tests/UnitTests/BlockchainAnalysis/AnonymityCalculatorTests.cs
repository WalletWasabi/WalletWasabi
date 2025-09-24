using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using Xunit;
using static WalletWasabi.Tests.UnitTests.BlockchainAnalysis.TransactionBuilder;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

public class AnonymityScoreDbTests
{
	[Fact]
	public void Update_WithNewCoin_AddsScoreAndPubKeyScore()
	{
		// Arrange
		var db = new AnonymityScoreDb();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), 1m);
		var score = 5.0m;

		// Act
		var updatedDb = db.SetAnonymityScore(coin, score);

		// Assert
		Assert.True(updatedDb.TryGetAnonymityScore(coin, out var anonymityScore));
		Assert.Equal(score, anonymityScore);
	}

	[Fact]
	public void TryGetAnonymityScore_WithNonExistingCoin_ReturnsFalse()
	{
		// Arrange
		var db = new AnonymityScoreDb();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), 1m);

		// Act
		var result = db.TryGetAnonymityScore(coin, out var anonymityScore);

		// Assert
		Assert.False(result);
		Assert.Equal(0, anonymityScore);
	}

	[Fact]
	public void Update_WithSamePubKeyLowerScore_UpdatesToMinimumScore()
	{
		// Arrange
		var db = new AnonymityScoreDb();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var pubKey = BitcoinFactory.CreateHdPubKey(km);
		var coin1 = BitcoinFactory.CreateSmartCoin(pubKey, 1m);
		var coin2 = BitcoinFactory.CreateSmartCoin(pubKey, 2m);
		var higherScore = 10.0m;
		var lowerScore = 5.0m;

		// Act
		var dbWithFirstCoin = db.SetAnonymityScore(coin1, higherScore);
		var dbWithSecondCoin = dbWithFirstCoin.SetAnonymityScore(coin2, lowerScore);

		// Assert
		Assert.True(dbWithSecondCoin.TryGetAnonymityScore(coin1, out var score1));
		Assert.True(dbWithSecondCoin.TryGetAnonymityScore(coin2, out var score2));
		Assert.Equal(lowerScore, score1); // Should be updated to minimum
		Assert.Equal(lowerScore, score2);
	}

	[Fact]
	public void Update_WithSamePubKeyHigherScore_KeepsMinimumScore()
	{
		// Arrange
		var db = new AnonymityScoreDb();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var pubKey = BitcoinFactory.CreateHdPubKey(km);
		var coin1 = BitcoinFactory.CreateSmartCoin(pubKey, 1m);
		var coin2 = BitcoinFactory.CreateSmartCoin(pubKey, 2m);
		var lowerScore = 5.0m;
		var higherScore = 10.0m;

		// Act
		var dbWithFirstCoin = db.SetAnonymityScore(coin1, lowerScore);
		var dbWithSecondCoin = dbWithFirstCoin.SetAnonymityScore(coin2, higherScore);

		// Assert
		Assert.True(dbWithSecondCoin.TryGetAnonymityScore(coin1, out var score1));
		Assert.True(dbWithSecondCoin.TryGetAnonymityScore(coin2, out var score2));
		Assert.Equal(lowerScore, score1); // Should remain at minimum
		Assert.Equal(lowerScore, score2); // Should be set to minimum
	}

	[Fact]
	public void Update_WithCoinHavingWalletInputs_AddsDependencies()
	{
		// Arrange
		var db = new AnonymityScoreDb();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var inputPubKey = BitcoinFactory.CreateHdPubKey(km);
		var outputPubKey = BitcoinFactory.CreateHdPubKey(km);
		var inputCoin = BitcoinFactory.CreateSmartCoin(inputPubKey, 1m);

		// Create a transaction with wallet inputs
		var tx0 = CreateTransaction([], [], [inputCoin], [(outputPubKey, Money.Coins(1m) )]);
		var outputCoin = tx0.WalletOutputs.First();

		// Act
		db = db
			.SetAnonymityScore(inputCoin, 3.0m)
			.SetAnonymityScore(outputCoin, 5.0m);

		// Assert
		Assert.True(db.TryGetAnonymityScore(inputCoin, out var inputCoinAnonymityScore));
		Assert.Equal(3.0m, inputCoinAnonymityScore);
		Assert.True(db.TryGetAnonymityScore(outputCoin, out var outputCoinAnonymityScore));
		Assert.Equal(5.0m, outputCoinAnonymityScore);

		// New output reusing address
		var tx1 = CreateTransaction([], [], [outputCoin], [(inputPubKey, inputCoin.Amount)]);
		var outputCoinReusingAddress = tx1.WalletOutputs.First();
		var invalidatedDb = db.SetAnonymityScore(outputCoinReusingAddress, 1.0m);

		// Assert - The dependency should be created, affecting cache invalidation behavior
		Assert.True(invalidatedDb.TryGetAnonymityScore(inputCoin, out inputCoinAnonymityScore));

		// The anonymity score is equal the the lowest AS score
		Assert.Equal(1.0m, inputCoinAnonymityScore);

		// The anonymity score for all dependent coins is invalidated (unknown) and needs to be recomputed using the new infoemation
		Assert.False(invalidatedDb.TryGetAnonymityScore(outputCoin, out _));
		Assert.True(invalidatedDb.TryGetAnonymityScore(outputCoinReusingAddress, out var outputCoinReusingAddressScore));
		Assert.Equal(1.0m, outputCoinReusingAddressScore);
	}

	[Fact]
	public void Update_WithDifferentCoins_MaintainsIndependentScores()
	{
		// Arrange
		var db = new AnonymityScoreDb();
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coin1 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), 1m);
		var coin2 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), 2m);
		var score1 = 5.0m;
		var score2 = 10.0m;

		// Act
		var updatedDb = db.SetAnonymityScore(coin1, score1).SetAnonymityScore(coin2, score2);

		// Assert
		Assert.True(updatedDb.TryGetAnonymityScore(coin1, out var result1));
		Assert.True(updatedDb.TryGetAnonymityScore(coin2, out var result2));
		Assert.Equal(score1, result1);
		Assert.Equal(score2, result2);
	}
}

public class AnalyzerTests
{
	public class Receive
	{
		[Fact]
		public void NormalReceive()
		{
			// Arrange
			var receiveTx = CreateTransaction(foreignInputCount: 2, foreignOutputCount: 1,
				walletInputCount: 0, walletOutputCount: 1);
			var receivedCoin = receiveTx.WalletOutputs.First();

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(receivedCoin, AnonymityScoreDb.Empty);

			// Assert
			Assert.Equal(XConstants.CertainlyKnown, score); // Default score for receive transactions
			Assert.True(resultDb.TryGetAnonymityScore(receivedCoin, out _));
		}

		[Fact]
		public void WholeCoinReceive()
		{
			// Arrange
			var receiveTx = CreateTransaction(foreignInputCount: 1, foreignOutputCount: 0,
				walletInputCount: 0, walletOutputCount: 1);
			var receivedCoin = receiveTx.WalletOutputs.First();

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(receivedCoin, AnonymityScoreDb.Empty);

			// Assert
			Assert.Equal(XConstants.CertainlyKnown, score); // Default score for receive transactions
			Assert.True(resultDb.TryGetAnonymityScore(receivedCoin, out _));
		}

		[Fact]
		public void ReceiveInCoinjoin()
		{
			// Arrange
			var coinjoinTx = CreateTransaction(foreignInputCount: 10, foreignOutputCount: 9,
				walletInputCount: 0, walletOutputCount: 1);
			var receivedCoin = coinjoinTx.WalletOutputs.First();

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(receivedCoin, AnonymityScoreDb.Empty);

			// Assert
			Assert.Equal(XConstants.CertainlyKnown, score); // Default score for receive transactions
			Assert.True(resultDb.TryGetAnonymityScore(receivedCoin, out _));
		}
	}

	public class Spend
	{
		[Fact]
		public void SpendAndReceiveChange()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 1, walletInputCount: 1, walletOutputCount: 1);
			var spentInput = spendingTx.WalletInputs.First();
			var receivedChange = spendingTx.WalletOutputs.First();
			var db = AnonymityScoreDb.Empty.SetAnonymityScore(spentInput, 1/3m);

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(receivedChange, db);

			// Assert
			Assert.Equal(XConstants.CertainlyKnown, score); // Default score for receive transactions
			Assert.True(resultDb.TryGetAnonymityScore(receivedChange, out _));
		}

		[Fact]
		public void SpendManyAndReceiveChange()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 1, walletInputCount: 3, walletOutputCount: 1);
			var spentInputs = spendingTx.WalletInputs.ToArray();
			var receivedChange = spendingTx.WalletOutputs.First();
			var db = spentInputs.Aggregate(AnonymityScoreDb.Empty, (scoreDb, coin) => scoreDb.SetAnonymityScore(coin, 1/3m));

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(receivedChange, db);

			// Assert
			Assert.Equal(XConstants.CertainlyKnown, score); // Default score for receive transactions
			Assert.True(resultDb.TryGetAnonymityScore(receivedChange, out _));
		}

		[Fact]
		public void SpendWholeCoin()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 1, walletInputCount: 1, walletOutputCount: 0);
			var spentInput = spendingTx.WalletInputs.First();
			var db = AnonymityScoreDb.Empty.SetAnonymityScore(spentInput, 1/10m);

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(spentInput, db);

			// Assert
			// Wasabi spends all reused addresses together. For this reason we can't spend reused scripts in the future.
			// However, we could receive money to already used addresses, but in that case the anonymity score of the
			// public key would be reset to 1.
			Assert.Equal(1/10m, score);
			Assert.True(resultDb.TryGetAnonymityScore(spentInput, out _));
		}

		[Fact]
		public void SpendWholeCoins()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 1, walletInputCount: 3, walletOutputCount: 0);
			var spentInputs = spendingTx.WalletInputs.ToArray();

			// Act
			var db = spentInputs.Aggregate(AnonymityScoreDb.Empty, (scoreDb, coin) => scoreDb.SetAnonymityScore(coin, 1/3m));

			// Assert
			Assert.All(spentInputs, s =>
			{
				Assert.True(db.TryGetAnonymityScore(s, out var score));
				Assert.Equal(1/3m, score);
			});
		}
	}

	public class SelfSpend
	{
		[Fact]
		public void SpendWholeCoin()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 0, walletInputCount: 1, walletOutputCount: 1);
			var spentInput = spendingTx.WalletInputs.First();
			var receivedOutput = spendingTx.WalletOutputs.First();

			// Act
			var db = AnonymityScoreDb.Empty.SetAnonymityScore(spentInput, 1/3m);

			// Assert
			// Anonset of the input shall be retained.
			// Although the tx has more than one interpretation
			// blockchain anal usually just assumes it's a self spend.
			Assert.Equal(1/3m, AnonymityCalculator.GetAnonymityScore(receivedOutput, db).AnonymityScore);
			Assert.Equal(1/3m, AnonymityCalculator.GetAnonymityScore(spentInput, db).AnonymityScore);
		}

		[Fact]
		public void SpendMultipleOwnOutputs()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 0, walletInputCount: 1, walletOutputCount: 3);
			var spentInput = spendingTx.WalletInputs.First();
			var receivedOutputs = spendingTx.WalletOutputs.ToArray();

			// Act
			var db = AnonymityScoreDb.Empty.SetAnonymityScore(spentInput, 1/3m);

			// Assert
			// Anonset of the input shall be retained.
			// Although the tx has many interpretations we shall not guess which one
			// a blockchain analyzer would go with, therefore outputs shall not gain anonsets
			// as we're conservatively estimating.
			Assert.All(receivedOutputs, c => Assert.Equal(1/3m, AnonymityCalculator.GetAnonymityScore(c, db).AnonymityScore));
		}

		[Fact]
		public void SpendManyInOne()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 0, walletInputCount: 3, walletOutputCount: 1);
			var spentInputs = spendingTx.WalletInputs.ToArray();
			var receivedOutput = spendingTx.WalletOutputs.First();

			// Act
			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInputs[0], 0.3m)
				.SetAnonymityScore(spentInputs[1], 0.3m)
				.SetAnonymityScore(spentInputs[2], 0.3m);

			// Assert
			// Anonset of the input shall be worsened because of input merging.
			Assert.Equal(0.9m, AnonymityCalculator.GetAnonymityScore(receivedOutput, db).AnonymityScore);
		}

		[Fact]
		public void SpendManyInMany()
		{
			// Arrange
			var spendingTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 0, walletInputCount: 3, walletOutputCount: 3);
			var spentInputs = spendingTx.WalletInputs.ToArray();
			var receivedOutputs = spendingTx.WalletOutputs.ToArray();

			// Act
			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInputs[0], 0.1m)
				.SetAnonymityScore(spentInputs[1], 0.1m)
				.SetAnonymityScore(spentInputs[2], 0.1m);

			// Assert
			// Anonset of the input shall be worsened because of input merging.
			Assert.All(receivedOutputs, c => Assert.Equal(0.3m, AnonymityCalculator.GetAnonymityScore(c, db).AnonymityScore));
		}
	}

	public class AddressReuse
	{
		[Fact]
		public void AddressReusePunishment()
		{
			// If there's reuse in input and output side, then output side didn't gain, nor lose anonymity.
			var tx = CreateTransaction(foreignInputCount: 9, foreignOutputCount: 9, walletInputCount: 1, walletOutputCount: 1);
			var spentInput = tx.WalletInputs.First();
			var receivedOutput = tx.WalletOutputs.First();
			var reusedAddress = BitcoinFactory.CreateSmartCoin(receivedOutput.HdPubKey, Money.Coins(1));

			// Make the reused key anonymity set something smaller than 109 (which should be the final anonymity set)
			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInput, 1/3m)
				.SetAnonymityScore(reusedAddress, 1/3m);

			// It should be smaller than 30, because reuse also gets punishment.
			Assert.Equal(1/3m, AnonymityCalculator.GetAnonymityScore(receivedOutput, db).AnonymityScore);
		}

		[Fact]
		public void AddressReusePunishmentProcessedTwice()
		{
			// If there's reuse in input and output side, then output side didn't gain, nor lose anonymity.
			var tx = CreateTransaction(foreignInputCount: 9, foreignOutputCount: 9, walletInputCount: 1, walletOutputCount: 1);
			var spentInput = tx.WalletInputs.First();
			var receivedOutput = tx.WalletOutputs.First();
			var reusedAddress = BitcoinFactory.CreateSmartCoin(receivedOutput.HdPubKey, Money.Coins(1));

			// Make the reused key anonymity set something smaller than 109 (which should be the final anonymity set)
			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInput, 1/3m)
				.SetAnonymityScore(reusedAddress, 1/3m);

			// It should be smaller than 30, because reuse also gets punishment.
			var (anonscore1, pdb1) = AnonymityCalculator.GetAnonymityScore(receivedOutput, db);
			var (anonscore2, pdb2) = AnonymityCalculator.GetAnonymityScore(receivedOutput, pdb1);
			Assert.Equal(1/3m, anonscore1);
			Assert.Equal(1/3m, anonscore2);
		}

		[Fact]
		public void AddressReuseIrrelevantInNormalSpend()
		{
			// In normal transactions we expose to someone that we own the inputs and the changes
			// So we cannot test address reuse here, because anonsets would be 1 regardless of anything.
			var km = KeyManager.CreateNew(out _, "", Network.Main);
			var reusedHdPubKey = BitcoinFactory.CreateHdPubKey(km);
			var receiveHdPubKey = BitcoinFactory.CreateHdPubKey(km);
			var tx = CreateTransaction(
				foreignInputs: Inputs(0),
				foreignOutputs: Outputs(1),
				walletInputs: Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateSmartCoin(reusedHdPubKey, 1m)),
				walletOutputs: [(receiveHdPubKey, Money.Coins(1m))]);
			var spentInputs = tx.WalletInputs.ToArray();
			var receivedOutput = tx.WalletOutputs.First();

			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInputs[0], 1 / 3m)
				.SetAnonymityScore(spentInputs[1], 1 / 3m)
				.SetAnonymityScore(spentInputs[2], 1 / 3m);

			// The receiver knows our change output
			var (anonscore, _) = AnonymityCalculator.GetAnonymityScore(receivedOutput, db);
			Assert.Equal(XConstants.CertainlyKnown, anonscore);
		}


		[Fact]
		public void InputSideAddressReuseHaveNoConsolidationPunishmentInSelfSpend()
		{
			// Consolidation can't hurt any more than reuse already has.
			var km = ServiceFactory.CreateKeyManager();
			var reusedHdPubKey = BitcoinFactory.CreateHdPubKey(km);
			var receiveHdPubKey = BitcoinFactory.CreateHdPubKey(km);
			var tx = CreateTransaction(
				foreignInputs: [],
				foreignOutputs: [],
				walletInputs: Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateSmartCoin(reusedHdPubKey, 1m)).ToArray(),
				walletOutputs: [(receiveHdPubKey, Money.Coins(1m))]);

			var spentInputs = tx.WalletInputs.ToArray();
			var receivedOutput = tx.WalletOutputs.First();

			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInputs[0], 1 / 3m)
				.SetAnonymityScore(spentInputs[1], 1 / 3m)
				.SetAnonymityScore(spentInputs[2], 1 / 3m);

			var (anonscore, _) = AnonymityCalculator.GetAnonymityScore(receivedOutput, db);
			Assert.Equal(1/3m, anonscore);
		}

		[Fact]
		public void InputSideAddressReuseHaveNoConsolidationPunishmentInCoinJoin()
		{
			var km = ServiceFactory.CreateKeyManager();
			var reusedHdPubKey = BitcoinFactory.CreateHdPubKey(km);
			var receiveHdPubKey = BitcoinFactory.CreateHdPubKey(km);
			var tx = CreateTransaction(
				foreignInputs: Inputs(10),
				foreignOutputs: Outputs(10),
				walletInputs: Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateSmartCoin(reusedHdPubKey, 1m)).ToArray(),
				walletOutputs: [(receiveHdPubKey, Money.Coins(1m))]);

			var spentInputs = tx.WalletInputs.ToArray();
			var receivedOutput = tx.WalletOutputs.First();

			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(spentInputs[0], 1 / 3m)
				.SetAnonymityScore(spentInputs[1], 1 / 3m)
				.SetAnonymityScore(spentInputs[2], 1 / 3m);

			var (anonscore, _) = AnonymityCalculator.GetAnonymityScore(receivedOutput, db);
			Assert.Equal((1/3m) / 10, anonscore);
		}
	}

	public class Multiparty
	{
		[Fact]
		public void BasicCalculation()
		{
			var tx = CreateTransaction(foreignInputCount: 10, foreignOutputCount: 10, walletInputCount: 1, walletOutputCount: 1);
			var receivedCoin = tx.WalletOutputs.First();

			var (score, db) = AnonymityCalculator.GetAnonymityScore(receivedCoin, AnonymityScoreDb.Empty);

			// 10 participants, 1 is you, your anonset is 10.
			Assert.Equal(1/10m, score);
		}

		[Fact]
		public void DoubleProcessing()
		{
			var tx = CreateTransaction(foreignInputCount: 10, foreignOutputCount: 10, walletInputCount: 1, walletOutputCount: 1);
			var receivedCoin = tx.WalletOutputs.First();

			var (score1, db) = AnonymityCalculator.GetAnonymityScore(receivedCoin, AnonymityScoreDb.Empty);
			var (score2, _) = AnonymityCalculator.GetAnonymityScore(receivedCoin, db);

			// 10 participants, 1 is you, your anonset is 10.
			Assert.Equal(1/10m, score1);
			Assert.Equal(1/10m, score2);
		}

		[Fact]
		public void Inheritance()
		{
			var tx = CreateTransaction(foreignInputCount: 10, foreignOutputCount: 10, walletInputCount: 1, walletOutputCount: 1);
			var spentCoin = tx.WalletInputs.First();
			var receivedCoin = tx.WalletOutputs.First();

			var db = AnonymityScoreDb.Empty.SetAnonymityScore(spentCoin, 1 / 100m);
			var (score, _) = AnonymityCalculator.GetAnonymityScore(receivedCoin, db);

			// 10 participants, 1 is you, your anonset is 1/10 and you inherit 1/100 anonset,
			// because you don't want to count yourself twice.
			Assert.Equal(1 / 1_000m, score);
		}


		[Fact]
		public void ChangeOutput()
		{
			var tx = CreateTransaction(
				Inputs(10),
				Outputs(10),
				InputCoins(1),
				OutputCoins(Money.Coins(1m), Money.Coins(5m)));

			var walletOutputs = tx.WalletOutputs.ToArray();
			var active = walletOutputs[0];
			var change = walletOutputs[1];

			var (activeScore, _) = AnonymityCalculator.GetAnonymityScore(active, AnonymityScoreDb.Empty);
			var (changeScore, _) = AnonymityCalculator.GetAnonymityScore(change, AnonymityScoreDb.Empty);

			Assert.Equal(1/10m, activeScore);
			Assert.Equal(1m, changeScore);
		}


		[Fact]
		public void ChangeOutputConservativeConsolidation()
		{
			var tx = CreateTransaction(
				Inputs(10),
				Outputs(10),
				InputCoins(2),
				OutputCoins(Money.Coins(1m), Money.Coins(5m)));
			var walletInputs = tx.WalletInputs.ToArray();
			var walletOutputs = tx.WalletOutputs.ToArray();
			var active = walletOutputs[0];
			var change = walletOutputs[1];

			var db = AnonymityScoreDb.Empty
				.SetAnonymityScore(walletInputs[0], 1 / 100m)
				.SetAnonymityScore(walletInputs[1], 1m);
			var (activeScore, _) = AnonymityCalculator.GetAnonymityScore(active, db);
			var (changeScore, _) = AnonymityCalculator.GetAnonymityScore(change, db);

			Assert.Equal(1/10m, activeScore);
			Assert.Equal(1m, changeScore);
		}

		[Fact]
		public void ChangeOutputInheritance()
		{
			var tx = CreateTransaction(
				Inputs(10),
				Outputs(10),
				InputCoins(1),
				OutputCoins(Money.Coins(1m), Money.Coins(5m)));
			var walletOutputs = tx.WalletOutputs.ToArray();

			var db = AnonymityScoreDb.Empty.SetAnonymityScore(tx.WalletInputs.First(), 1 / 100m);
			var (activeScore, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[0], db);
			var (changeScore, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[1], db);

			Assert.Equal(1 / 1000m, activeScore);
			Assert.Equal(1 / 100m, changeScore);
		}

		[Fact]
		public void MultiDenomination()
		{
			// Multiple standard denomination outputs should be accounted separately.
			var tx = CreateTransaction(
				Inputs(10),
				Outputs(Money.Coins(1m), Money.Coins(1m), Money.Coins(1m), Money.Coins(2m), Money.Coins(2m)),
				InputCoins(1),
				OutputCoins(Money.Coins(1m), Money.Coins(2m)));
			var walletOutputs = tx.WalletOutputs.ToArray();

			var (denomScore1, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[0], AnonymityScoreDb.Empty);
			var (denomScore2, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[1], AnonymityScoreDb.Empty);

			Assert.Equal(1 / 3m, denomScore1);
			Assert.Equal(1 / 2m, denomScore2);
		}


		[Fact]
		public void MultiDenominationInheritance()
		{
			// Multiple denominations inherit properly.
			// Multiple standard denomination outputs should be accounted separately.
			var tx = CreateTransaction(
				Inputs(10),
				Outputs(Money.Coins(1m), Money.Coins(1m), Money.Coins(1m), Money.Coins(2m), Money.Coins(2m)),
				InputCoins(1),
				OutputCoins(Money.Coins(1m), Money.Coins(2m)));
			var walletOutputs = tx.WalletOutputs.ToArray();
			var walletInput = tx.WalletInputs.First();

			var db = AnonymityScoreDb.Empty.SetAnonymityScore(walletInput, 1 / 100m);
			var (denomScore1, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[0], db);
			var (denomScore2, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[1], db);

			Assert.Equal(1 / 3m / 100m, denomScore1);
			Assert.Equal(1 / 2m / 100m, denomScore2);
		}

		[Fact]
		public void SelfAnonsetSanityCheck()
		{
			// If we have multiple same denomination in the same coinjoin, then our anonset would be total coins/our coins.
			var tx = CreateTransaction(
				Inputs(9),
				Outputs(Money.Coins(1m), Money.Coins(1m), Money.Coins(1m)),
				InputCoins(Money.Coins(3.2m)),
				OutputCoins(Money.Coins(1m), Money.Coins(1m)));
			var walletOutputs = tx.WalletOutputs.ToArray();

			var (denomScore1, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[0], AnonymityScoreDb.Empty);
			var (denomScore2, _) = AnonymityCalculator.GetAnonymityScore(walletOutputs[1], AnonymityScoreDb.Empty);

			Assert.Equal(2 / 3m, denomScore1);
			Assert.Equal(2 / 3m, denomScore2);
		}

		[Fact]
		public void SelfAnonsetSanityCheck2()
		{
			var tx = CreateTransaction(
				Inputs(1),
				Outputs(Money.Coins(1m)),
				InputCoins(Money.Coins(4.2m)),
				OutputCoins(Money.Coins(1m), Money.Coins(1m), Money.Coins(1m), Money.Coins(1m)));

			var db = AnonymityScoreDb.Empty.SetAnonymityScore(tx.WalletInputs.First(), 1 / 10m);

			var anonScores = tx.WalletOutputs
				.Select(output => AnonymityCalculator.GetAnonymityScore(output, db))
				.Select(x => x.AnonymityScore);

			// The increase in the anonymity set would naively be 1 as there is 1 equal non-wallet output.
			// Since 4 outputs are ours, we divide the increase in anonymity between them
			// and add that to the inherited anonymity of 4.
			Assert.All(anonScores, x => Assert.Equal(1 / 10m / 4m, x));
		}

		[Fact]
		public void InputSanityCheck()
		{
			// Anonset can never be larger than the number of our inputs.
			var tx = CreateTransaction(
				Inputs(2),
				Outputs(9),
				InputCoins(Money.Coins(1.1m)),
				OutputCoins(Money.Coins(1m)));

			var (anonScore, _) = AnonymityCalculator.GetAnonymityScore(tx.WalletOutputs.First(), AnonymityScoreDb.Empty);
			Assert.Equal(1 / 2m, anonScore);
		}

		[Fact]
		public void SelfAnonsetSanityBeforeInputSanityCheck()
		{
			// Self anonset sanity check is executed before input sanity check is executed.
			var tx = CreateTransaction(
				Inputs(1),
				Outputs(Money.Coins(1m), Money.Coins(1m), Money.Coins(1m)),
				InputCoins(Money.Coins(3.2m)),
				OutputCoins(Money.Coins(1), Money.Coins(1)));

			var (anonScore, _) = AnonymityCalculator.GetAnonymityScore(tx.WalletOutputs.First(), AnonymityScoreDb.Empty);

			// The other guy in the coinjoin knows my coins
			Assert.Equal(1, anonScore);
		}

		[Fact]
		public void InputMergePunishmentNoInheritance()
		{
			// Input merging results in worse inherited anonset, but does not punish gains from output indistinguishability.
			var tx = CreateTransaction(
				Inputs(10),
				Outputs(10),
				InputCoins(Money.Coins(1.1m), Money.Coins(1.2m), Money.Coins(1.3m), Money.Coins(1.4m)),
				OutputCoins(Money.Coins(1)));

			var (anonScore, _) = AnonymityCalculator.GetAnonymityScore(tx.WalletOutputs.First(), AnonymityScoreDb.Empty);

			// 10 participants, 1 is you, your anonset would be 10 normally and now too:
			Assert.Equal(1 / 10m, anonScore);
		}
	}

	public class General
	{
		[Fact]
		public void GetAnonymityScore_WithCachedScore_ReturnsCachedValue()
		{
			// Arrange
			var km = KeyManager.CreateNew(out _, "", Network.Main);
			var coin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1));
			var cachedScore = 15.0m;
			var db = AnonymityScoreDb.Empty.SetAnonymityScore(coin, cachedScore);

			// Act
			var (score, resultDb) = AnonymityCalculator.GetAnonymityScore(coin, db);

			// Assert
			Assert.Equal(cachedScore, score);
			Assert.Same(db, resultDb);
		}

		[Fact]
		public void AnalyzeCoinAnonymity_SelfSpendTransaction_ReturnsIntersectionOfInputScores()
		{
			// Arrange
			var receiveTx = CreateTransaction(foreignInputCount: 0, foreignOutputCount: 0,
				walletInputCount: 2, walletOutputCount: 1);
			var spentCoins = receiveTx.WalletInputs.ToArray();
			var receivedCoin = receiveTx.WalletOutputs.First();

			var db = new AnonymityScoreDb()
				.SetAnonymityScore(spentCoins[0], .1m)
				.SetAnonymityScore(spentCoins[1], .08m);

			// Act
			var (score, _) = AnonymityCalculator.GetAnonymityScore(receivedCoin, db);

			Assert.Equal(0.18m, score);
		}

		[Fact]
		public void AnalyzeCoinAnonymity_MultipartyTransaction_ReturnsMinimumPlusGain()
		{
			// Arrange
			var tx = CreateTransaction(foreignInputCount: 10, foreignOutputCount: 10,
				walletInputCount: 1, walletOutputCount: 2);
			var inputCoin = tx.WalletInputs.First();
			var outputCoins = tx.WalletOutputs.ToArray();

			var db = new AnonymityScoreDb().SetAnonymityScore(inputCoin, .2m);

			// Act
			var (score1, _) = AnonymityCalculator.GetAnonymityScore(outputCoins[0], db);
			var (score2, _) = AnonymityCalculator.GetAnonymityScore(outputCoins[1], db);

			// Assert
			// Should be minimum score (5) plus anonymity gain (10 foreign outputs / 2 wallet outputs)
			Assert.Equal(.04m, score1);
			Assert.Equal(.04m, score2);
		}
	}
}

