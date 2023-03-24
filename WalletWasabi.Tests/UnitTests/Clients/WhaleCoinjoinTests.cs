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

	private IEnumerable<SmartCoin> SelectSpecificClient(IEnumerable<IEnumerable<SmartCoin>> clients)
	{
		return clients.MaxBy(x => x.Sum(y => y.Amount))!;
	}

	[Theory]
	[InlineData(1234)]
	[InlineData(6789)]
	public void ScoreForWhales(int randomSeed)
	{
		var nbClients = 30;
		var otherNbInputsPerClient = 5;
		var anonScoreTarget = 100;
		var maxTestRounds = 1000;


		List<bool> mode = new() { true, false };
		foreach (var master in mode)
		{
			Money maxSuggestedAmount = new(1343.75m, MoneyUnit.BTC);
			Money liquidityClue = LiquidityClueProvider.GetLiquidityClue(maxSuggestedAmount);

			Func<IEnumerable<IEnumerable<SmartCoin>>, IEnumerable<SmartCoin>> formulaSpecificClient = SelectSpecificClient;
			var mockSecureRandom = new TestRandomSeed(randomSeed);

			var displayProgressEachNRounds = int.MaxValue;

			var analyser = new BlockchainAnalyzer();

			RoundParameters roundParams = CreateMultipartyTransactionParameters();
			KeyManager km = KeyManager.CreateNew(out _, "", Network.RegTest);
			HdPubKey[] otherHdPub = new HdPubKey[nbClients];
			for (int i = 0; i < nbClients; i++)
			{
				otherHdPub[i] = BitcoinFactory.CreateHdPubKey(km);
			}


			List<List<SmartCoin>> otherSmartCoins = CreateOtherSmartCoins(mockSecureRandom, otherHdPub, otherNbInputsPerClient);
			List<SmartCoin> specificClientSmartCoins = formulaSpecificClient(otherSmartCoins).ToList();
			otherSmartCoins.RemoveAll(x => x.All(y => specificClientSmartCoins.Contains(y)) && specificClientSmartCoins.All(z => x.Contains(z)));
			otherHdPub = otherHdPub.Take(otherHdPub.Length - 1).ToArray();
			var specificClientStartingBalance = specificClientSmartCoins.Sum(x => x.Amount);
			if (specificClientStartingBalance < liquidityClue)
			{
				TestOutputHelper.WriteLine("WARNING: Specific Client does not have enough funds to see the behavior we want to test. Please change the seed");
				break;
			}
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

				whaleMinInputAnonSet = specificClientSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
				var whaleTotalSatoshiBefore = specificClientSmartCoins.Sum(x => x.Amount.Satoshi);
				var whaleGlobalAnonScoreBefore = specificClientSmartCoins.Sum(x => (x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / whaleTotalSatoshiBefore) / anonScoreTarget;

				// Same as cost variable in pr #8938
				var whaleCostCoinsBefore = specificClientSmartCoins.Select(x => (x.HdPubKey.AnonymitySet - whaleMinInputAnonSet) * x.Amount.Satoshi);

				var whaleSelectedSmartCoins = SelectCoinsForRound(specificClientSmartCoins, anonScoreTarget, roundParams, mockSecureRandom, master, liquidityClue);
				if (!whaleSelectedSmartCoins.Select(sm => sm.Coin).Any())
				{
					break;
				}

				specificClientSmartCoins.Remove(whaleSelectedSmartCoins);

				otherSmartCoins = CreateOtherSmartCoins(mockSecureRandom, otherHdPub, otherNbInputsPerClient);
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

				specificClientSmartCoins.Add(tx.WalletOutputs.ToList());

				whaleMinInputAnonSet = specificClientSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
				var whaleTotalSatoshiAfter = specificClientSmartCoins.Sum(x => x.Amount.Satoshi);
				whaleGlobalAnonScoreAfter = specificClientSmartCoins.Sum(x => (x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / whaleTotalSatoshiAfter) / anonScoreTarget;

				// Same as cost variable in pr #8938
				var whaleCostCoinsAfter = specificClientSmartCoins.Select(x => (x.HdPubKey.AnonymitySet - whaleMinInputAnonSet) * x.Amount.Satoshi);

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
					DisplayCoins(specificClientSmartCoins);
				}
			}

			WriteLine($"master: {master}, whaleAmountBtc: {specificClientStartingBalance}, liquidityClue: {LiquidityClueProvider.GetLiquidityClue(maxSuggestedAmount)}, randomSeed: {randomSeed}");
			WriteLine(
				counter >= maxTestRounds
					? $"FAILED whaleMaxGlobalAnonScore only reached {Math.Round(whaleMaxGlobalAnonScore * 100, 2)} after {counter} rounds"
					: $"PASSED after {counter} rounds -> Total vSize Whale: {totalVSizeWhale}");
			DisplayAnonScore(whaleGlobalAnonScoreAfter, whaleMaxGlobalAnonScore, maxDeltaGlobalAnonScore, minDeltaGlobalAnonScore);
		}
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

	private static List<List<SmartCoin>> CreateOtherSmartCoins(TestRandomSeed rnd, IReadOnlyList<HdPubKey> otherHdPub, int otherNbInputsPerClient)
	{
		var nbOtherClients = otherHdPub.Count;
		var otherSmartCoins = new List<List<SmartCoin>>();
		var otherIndex = 0;

		var inputs = GetSakeInputs();
		for (var i = 0; i < nbOtherClients; i++)
		{
			var listCoinsCurrentOther = new List<SmartCoin>();
			for (var j = 0; j < otherNbInputsPerClient; j++)
			{
				listCoinsCurrentOther.Add(BitcoinFactory.CreateSmartCoin(otherHdPub[otherIndex], inputs.RandomElement(rnd)));
			}

			otherIndex++;
			otherSmartCoins.Add(listCoinsCurrentOther);
		}

		return otherSmartCoins;
	}

	private static List<decimal> GetSakeInputs()
	{
		// Get the path of the file
		string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../UnitTests/Clients/SakeInputs.txt");
		string[] lines = File.ReadAllLines(filePath);
		List<decimal> numbers = new List<decimal>();
		foreach (var line in lines)
		{
			numbers.Add(decimal.Parse(line));
		}
		return numbers;
	}

	private static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> sc, int anonScoreTarget, RoundParameters roundParams, TestRandomSeed mockSecureRandom, bool master, Money liquidityClue)
	{
		return CoinJoinClient.SelectCoinsForRound(
			coins: sc,
			UtxoSelectionParameters.FromRoundParameters(roundParams),
			consolidationMode: false,
			anonScoreTarget: anonScoreTarget,
			liquidityClue: liquidityClue,
			semiPrivateThreshold: 2,
			rnd: mockSecureRandom,
			master: master);
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

		var amountDecomposer = new AmountDecomposer(feeRate, allowedOutputAmountRange, Constants.P2trOutputVirtualSize * 8, true, rnd);
		var whaleDecomposed = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);
		return whaleDecomposed.Select(output => (output.Amount, HdPubKey.DefaultHighAnonymitySet)).ToList();
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
