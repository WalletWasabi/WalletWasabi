using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi;
using Xunit;
using WalletWasabi.Helpers;
using WalletWasabi.Extensions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Rounds;
using DynamicData;
using WalletWasabi.Blockchain.TransactionOutputs;
using Xunit.Abstractions;
using System.IO;
using System.Threading;

namespace WalletWasabi.Tests.UnitTests.Clients;

public class WhaleCoinjoinTests
{
	private ITestOutputHelper TestOutputHelper { get; }
	private string OutputFilePath { get; }
	private LiquidityClueProvider LiquidityClueProvider { get; }

	public WhaleCoinjoinTests(ITestOutputHelper testOutputHelper)
	{
		TestOutputHelper = testOutputHelper;
		var outputFolder = Directory.CreateDirectory(Common.GetWorkDir(nameof(WhaleCoinjoinTests), "Output"));
		var date = DateTime.Now.ToString("Md_HHmmss");
		OutputFilePath = Path.Combine(outputFolder.FullName, $"Outputs{date}.txt");
		LiquidityClueProvider = new();
	}

	[Theory]
	[InlineData(true, 1, 0.0005, 0.05, 1234)]
	[InlineData(true, 1, 0.0005, 0.05, 6789)]
	[InlineData(true, 1, 0.001, 0.1, 1234)]
	[InlineData(true, 1, 0.0005, 0.2, 1234)]
	[InlineData(false, 1, 0.0005, 0.05, 1234)]
	[InlineData(false, 1, 0.0005, 0.05, 6789)]
	[InlineData(false, 1, 0.001, 0.1, 1234)]
	[InlineData(false, 1, 0.0005, 0.2, 1234)]
	public void ScoreForWhales(bool master, double whaleAmountBtc, double otherClientsAmountMin, double otherClientsAmountMax, int randomSeed)
	{
		var nbOtherClients = 30;
		var otherNbInputsPerClient = 5;
		var mockSecureRandom = new TestRandomSeed(randomSeed);

		Money maxSuggestedAmount = new(1343.75m, MoneyUnit.BTC);

		var anonScoreTarget = 100;
		var maxTestRounds = 1000;
		var displayProgressEachNRounds = int.MaxValue;

		var analyser = new BlockchainAnalyzer();

		RoundParameters roundParams = CreateMultipartyTransactionParameters();
		KeyManager km = KeyManager.CreateNew(out _, "", Network.RegTest);
		HdPubKey hdPub = BitcoinFactory.CreateHdPubKey(km);
		HdPubKey[] otherHdPub = new HdPubKey[nbOtherClients];
		for (int i = 0; i < nbOtherClients; i++)
		{
			KeyManager kmTmp = KeyManager.CreateNew(out _, "", Network.RegTest);
			otherHdPub[i] = BitcoinFactory.CreateHdPubKey(kmTmp);
		}

		var whaleSmartCoins = new List<SmartCoin> { BitcoinFactory.CreateSmartCoin(hdPub, (decimal)whaleAmountBtc) };
		var totalVSizeWhale = 0;
		var whaleMinInputAnonSet = 1.0;
		var whaleMaxGlobalAnonScore = 0.0;
		var maxDeltaGlobalAnonScore = 0.0;
		var minDeltaGlobalAnonScore = 0.0;
		var whaleGlobalAnonScoreAfter = 0.0;
		var counter = 0;
		while (whaleMinInputAnonSet < anonScoreTarget && counter <= maxTestRounds)
		{
			counter++;

			whaleMinInputAnonSet = whaleSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
			var whaleTotalSatoshiBefore = whaleSmartCoins.Sum(x => x.Amount.Satoshi);
			var whaleGlobalAnonScoreBefore = whaleSmartCoins.Sum(x => (x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / whaleTotalSatoshiBefore) / anonScoreTarget;
			// Same as cost variable in pr #8938
			var whaleCostCoinsBefore = whaleSmartCoins.Select(x => (x.HdPubKey.AnonymitySet - whaleMinInputAnonSet) * x.Amount.Satoshi);

			Money liquidityClue = LiquidityClueProvider.GetLiquidityClue(maxSuggestedAmount);

			var whaleSelectedSmartCoins = SelectCoinsForRound(whaleSmartCoins, anonScoreTarget, roundParams, mockSecureRandom, master, liquidityClue);
			if (!whaleSelectedSmartCoins.Select(sm => sm.Coin).Any())
			{
				break;
			}

			whaleSmartCoins.Remove(whaleSelectedSmartCoins);

			var otherSmartCoins = CreateOtherSmartCoins(mockSecureRandom, otherHdPub, otherNbInputsPerClient, otherClientsAmountMin, otherClientsAmountMax);
			var otherSelectedSmartCoins = new List<List<SmartCoin>>();
			var otherOutputs = new List<List<(Money, int)>>();

			foreach (var other in otherSmartCoins)
			{
				var tmpSelectedSmartCoins = SelectCoinsForRound(other, anonScoreTarget, roundParams, mockSecureRandom, master, liquidityClue);
				otherSelectedSmartCoins.Add((tmpSelectedSmartCoins.ToList()));
			}

			foreach (var otherSelectedSmartCoin in otherSelectedSmartCoins)
			{
				otherOutputs.Add(DecomposeWithAnonSet(otherSelectedSmartCoin.Select(x => x.Coin), otherSelectedSmartCoins.SelectMany(x => x).Except(otherSelectedSmartCoin).Concat(whaleSelectedSmartCoins).Select(x => x.Coin), mockSecureRandom));
			}

			var whaleInputs = whaleSelectedSmartCoins.Select(x => (x.Amount, (int)x.HdPubKey.AnonymitySet));
			var whaleOutputs = DecomposeWithAnonSet(whaleSelectedSmartCoins.Select(sm => sm.Coin), otherSelectedSmartCoins.SelectMany(x => x).Select(x => x.Coin), mockSecureRandom);
			var tx = BitcoinFactory.CreateSmartTransaction(otherSelectedSmartCoins.SelectMany(x => x).Count(), otherOutputs.SelectMany(x => x).Select(x => x.Item1), whaleInputs, whaleOutputs);

			analyser.Analyze(tx);

			whaleSmartCoins.Add(tx.WalletOutputs.ToList());

			whaleMinInputAnonSet = whaleSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
			var whaleTotalSatoshiAfter = whaleSmartCoins.Sum(x => x.Amount.Satoshi);
			whaleGlobalAnonScoreAfter = whaleSmartCoins.Sum(x => (x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / whaleTotalSatoshiAfter) / anonScoreTarget;
			// Same as cost variable in pr #8938
			var whaleCostCoinsAfter = whaleSmartCoins.Select(x => (x.HdPubKey.AnonymitySet - whaleMinInputAnonSet) * x.Amount.Satoshi);

			LiquidityClueProvider.UpdateLiquidityClue(maxSuggestedAmount, tx.Transaction, tx.WalletOutputs.Select(output => output.TxOut));

			if (whaleGlobalAnonScoreAfter > whaleMaxGlobalAnonScore)
			{
				whaleMaxGlobalAnonScore = whaleGlobalAnonScoreAfter;
			}

			var deltaGlobalAnonScore = whaleGlobalAnonScoreAfter - whaleGlobalAnonScoreBefore;
			if (deltaGlobalAnonScore > maxDeltaGlobalAnonScore)
			{
				maxDeltaGlobalAnonScore = deltaGlobalAnonScore;
			}

			if (deltaGlobalAnonScore < minDeltaGlobalAnonScore)
			{
				minDeltaGlobalAnonScore = deltaGlobalAnonScore;
			}

			totalVSizeWhale += tx.WalletInputs.Sum(x => x.ScriptPubKey.EstimateInputVsize());

			if (counter % displayProgressEachNRounds == 0)
			{
				WriteLine($"Round N° {counter} - Total vSize: {totalVSizeWhale}");
				DisplayAnonScore(whaleGlobalAnonScoreAfter, whaleMaxGlobalAnonScore, maxDeltaGlobalAnonScore, minDeltaGlobalAnonScore);
				DisplayCoins(whaleSmartCoins);
			}
		}

		WriteLine($"master: {master}, whaleAmountBtc: {whaleAmountBtc}, liquidityClue: {LiquidityClueProvider.GetLiquidityClue(maxSuggestedAmount)}, otherClientsAmountMin: {otherClientsAmountMin}, otherClientsAmountMax: {otherClientsAmountMax}, randomSeed: {randomSeed}");
		WriteLine(counter >= maxTestRounds
			? $"FAILED whaleMaxGlobalAnonScore only reached {Math.Round(whaleMaxGlobalAnonScore * 100, 2)} after {counter} rounds"
			: $"PASSED after {counter} rounds -> Total vSize Whale: {totalVSizeWhale}");
		DisplayAnonScore(whaleGlobalAnonScoreAfter, whaleMaxGlobalAnonScore, maxDeltaGlobalAnonScore, minDeltaGlobalAnonScore);
	}

	public class TestRandomSeed : SecureRandom
	{
		private readonly Random _seededInstance;

		public TestRandomSeed(int seed)
		{
			_seededInstance = new Random(seed);
		}

		public override int GetInt(int fromInclusive, int toExclusive)
		{
			return _seededInstance.Next(fromInclusive, toExclusive);
		}

		public double GetDouble()
		{
			return _seededInstance.NextDouble();
		}
	}

	private void WriteLine(string content)
	{
		TestOutputHelper.WriteLine(content);
		using StreamWriter sw = new(OutputFilePath, append: true);
		sw.WriteLine(content);
	}

	private void DisplayAnonScore(double currentAnonScore, double maxGlobalAnonScore, double maxDeltaGlobalAnonScore, double minDeltaGlobalAnonScore)
	{
		WriteLine($"AnonScore: Current: {Math.Round(currentAnonScore * 100, 2)}% Max: {Math.Round(maxGlobalAnonScore * 100, 2)}% - Delta: Max: +{Math.Round(maxDeltaGlobalAnonScore * 100, 2)}% / Min: {Math.Round(minDeltaGlobalAnonScore * 100, 2)}%");
	}

	private void DisplayCoins(IEnumerable<SmartCoin> whaleSmartCoins)
	{
		WriteLine($"Amount       AnonSet       ");
		var whaleSmartCoinsOrdered = whaleSmartCoins.OrderByDescending(x => (int)Math.Round(x.HdPubKey.AnonymitySet));
		foreach (var whaleSC in whaleSmartCoinsOrdered)
		{
			WriteLine($"{whaleSC.Amount} {(int)Math.Round(whaleSC.HdPubKey.AnonymitySet)}");
		}
	}

	private static List<List<SmartCoin>> CreateOtherSmartCoins(TestRandomSeed rnd, IReadOnlyList<HdPubKey> otherHdPub, int otherNbInputsPerClient, double otherClientsAmountMin, double otherClientsAmountMax)
	{
		var nbOtherClients = otherHdPub.Count;
		var otherSmartCoins = new List<List<SmartCoin>>();
		var otherIndex = 0;
		for (var i = 0; i < nbOtherClients; i++)
		{
			var listCoinsCurrentOther = new List<SmartCoin>();
			for (var j = 0; j < otherNbInputsPerClient; j++)
			{
				// Sleeping solves a weird inconsistency that sometimes occurs
				Thread.Sleep(1);
				var tmp = GetRandomDouble(rnd, otherClientsAmountMin, otherClientsAmountMax);
				listCoinsCurrentOther.Add(BitcoinFactory.CreateSmartCoin(otherHdPub[otherIndex], (decimal)tmp));
			}

			otherIndex++;
			otherSmartCoins.Add(listCoinsCurrentOther);
		}

		return otherSmartCoins;
	}

	private static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> sc, int anonScoreTarget, RoundParameters roundParams, TestRandomSeed mockSecureRandom, bool master, Money liquidityClue)
	{
		return CoinJoinClient.SelectCoinsForRound(
			coins: sc,
			roundParams,
			consolidationMode: false,
			anonScoreTarget: anonScoreTarget,
			redCoinIsolation: false,
			liquidityClue: liquidityClue,
			rnd: mockSecureRandom,
			master: master
		);
	}

	private static List<(Money, int)> DecomposeWithAnonSet(IEnumerable<Coin> ourCoins, IEnumerable<Coin> theirCoins, TestRandomSeed rnd)
	{
		decimal feeRateDecimal = 5;
		var minOutputAmount = 5000;
		var maxAvailableOutputs = 50;

		var availableVsize = maxAvailableOutputs * Constants.P2wpkhOutputVirtualSize;
		var feeRate = new FeeRate(feeRateDecimal);
		var feePerOutput = feeRate.GetFee(Constants.P2wpkhOutputVirtualSize);
		var registeredCoinEffectiveValues = ourCoins.Select(c => c.EffectiveValue(feeRate, CoordinationFeeRate.Zero)).ToList();
		var theirCoinEffectiveValues = theirCoins.Select(c => c.EffectiveValue(feeRate, CoordinationFeeRate.Zero)).ToList();
		var allowedOutputAmountRange = new MoneyRange(Money.Satoshis(minOutputAmount), Money.Satoshis(ProtocolConstants.MaxAmountPerAlice));

		var amountDecomposer = new AmountDecomposer(feeRate, allowedOutputAmountRange, Constants.P2wpkhOutputVirtualSize, Constants.P2wpkhInputVirtualSize, availableVsize, rnd);
		var whaleDecomposed = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);
		return whaleDecomposed.Select(output => (output, HdPubKey.DefaultHighAnonymitySet)).ToList();
	}

	private static RoundParameters CreateMultipartyTransactionParameters()
	{
		var roundParams = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = Money.Coins(0.00001m),
			MaxRegistrableAmount = Money.Coins(430)
		});
		return roundParams;
	}

	private static double GetRandomDouble(TestRandomSeed rnd, double min, double max, int precision = 6)
	{
		return rnd.GetInt((int)(Math.Pow(10, precision) * min), (int)(Math.Pow(10, precision) * max)) / Math.Pow(10, precision);
	}
}
