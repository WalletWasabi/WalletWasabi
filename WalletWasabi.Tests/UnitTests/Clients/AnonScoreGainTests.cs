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

public class AnonScoreGainTests
{
	private ITestOutputHelper TestOutputHelper { get; }
	private string OutputFilePath { get; }
	private LiquidityClueProvider LiquidityClueProvider { get; }

	public AnonScoreGainTests(ITestOutputHelper testOutputHelper)
	{
		TestOutputHelper = testOutputHelper;
		var outputFolder = Directory.CreateDirectory(Common.GetWorkDir(nameof(AnonScoreGainTests), "Output"));
		var date = DateTime.Now.ToString("Md_HHmmss");
		OutputFilePath = Path.Combine(outputFolder.FullName, $"Outputs{date}.txt");
		LiquidityClueProvider = new();
	}

	private IEnumerable<SmartCoin> SelectSpecificClient(IEnumerable<IEnumerable<SmartCoin>> clients)
	{
		return clients.MaxBy(x => x.Sum(y => y.Amount))!;
	}

	[Theory]
	[InlineData(443, 0.5, 50)]
	public void AnonScoreGainTest(int randomSeed, double p, double q)
	{
		var nbClients = 50;
		var otherNbInputsPerClient = 5;
		var anonScoreTarget = 100;
		var maxTestRounds = 1000;


		List<bool> mode = new() { true, false };
		foreach (var master in mode)
		{
			Money maxSuggestedAmount = new(1343.75m, MoneyUnit.BTC);

			Func<IEnumerable<IEnumerable<SmartCoin>>, IEnumerable<SmartCoin>> formulaSpecificClient = SelectSpecificClient;
			var mockSecureRandom = new TestRandomSeed(randomSeed);

			var displayProgressEachNRounds = 9999999999;

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

			var totalVsizeSpecificClient = 0;
			var specificClientMinInputAnonSet = 1.0;
			var specificClientMaxGlobalAnonScore = 0.0;
			var maxDeltaGlobalAnonScore = 0.0;
			var minDeltaGlobalAnonScore = 0.0;
			var specificClientGlobalAnonScoreAfter = 0.0;
			var counter = 0;
			while (specificClientMinInputAnonSet < anonScoreTarget && counter <= maxTestRounds)
			{
				counter++;
				Money liquidityClue = Math.Min(LiquidityClueProvider.GetLiquidityClue(maxSuggestedAmount), specificClientStartingBalance) - 1;
				specificClientMinInputAnonSet = specificClientSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
				var specificClientTotalSatoshiBefore = specificClientSmartCoins.Sum(x => x.Amount.Satoshi);
				var specificClientGlobalAnonScoreBefore = specificClientSmartCoins.Sum(x => (x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / specificClientTotalSatoshiBefore) / anonScoreTarget;

				var specificClientSelectedSmartCoins = SelectCoinsForRound(specificClientSmartCoins, anonScoreTarget, roundParams, mockSecureRandom, master, liquidityClue, p, q);
				if (!specificClientSelectedSmartCoins.Select(sm => sm.Coin).Any())
				{
					break;
				}

				specificClientSmartCoins.Remove(specificClientSelectedSmartCoins);

				otherSmartCoins = CreateOtherSmartCoins(mockSecureRandom, otherHdPub, otherNbInputsPerClient);
				var otherSelectedSmartCoins = new List<List<SmartCoin>>();
				var otherOutputs = new List<List<(Money, int)>>();

				foreach (var other in otherSmartCoins)
				{
					var tmpSelectedSmartCoins = SelectCoinsForRound(other, anonScoreTarget, roundParams, mockSecureRandom, master, liquidityClue, p, q);
					otherSelectedSmartCoins.Add((tmpSelectedSmartCoins.ToList()));
				}

				foreach (var otherSelectedSmartCoin in otherSelectedSmartCoins)
				{
					otherOutputs.Add(DecomposeWithAnonSet(otherSelectedSmartCoin.Select(x => x.Coin), otherSelectedSmartCoins.SelectMany(x => x).Except(otherSelectedSmartCoin).Concat(specificClientSelectedSmartCoins).Select(x => x.Coin), mockSecureRandom));
				}

				var specificClientInputs = specificClientSelectedSmartCoins.Select(x => (x.Amount, (int)x.HdPubKey.AnonymitySet));
				var specificClientOutputs = DecomposeWithAnonSet(specificClientSelectedSmartCoins.Select(sm => sm.Coin), otherSelectedSmartCoins.SelectMany(x => x).Select(x => x.Coin), mockSecureRandom);
				var tx = BitcoinFactory.CreateSmartTransaction(otherSelectedSmartCoins.SelectMany(x => x).Count(), otherOutputs.SelectMany(x => x).Select(x => x.Item1), specificClientInputs, specificClientOutputs);

				analyser.Analyze(tx);

				specificClientSmartCoins.Add(tx.WalletOutputs.ToList());

				specificClientMinInputAnonSet = specificClientSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
				var specificClientTotalSatoshiAfter = specificClientSmartCoins.Sum(x => x.Amount.Satoshi);
				specificClientGlobalAnonScoreAfter = specificClientSmartCoins.Sum(x => (x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / specificClientTotalSatoshiAfter) / anonScoreTarget;

				LiquidityClueProvider.UpdateLiquidityClue(maxSuggestedAmount, tx.Transaction, tx.WalletOutputs.Select(output => output.TxOut));

				if (specificClientGlobalAnonScoreAfter > specificClientMaxGlobalAnonScore)
				{
					specificClientMaxGlobalAnonScore = specificClientGlobalAnonScoreAfter;
				}

				var deltaGlobalAnonScore = specificClientGlobalAnonScoreAfter - specificClientGlobalAnonScoreBefore;
				if (deltaGlobalAnonScore > maxDeltaGlobalAnonScore)
				{
					maxDeltaGlobalAnonScore = deltaGlobalAnonScore;
				}

				if (deltaGlobalAnonScore < minDeltaGlobalAnonScore)
				{
					minDeltaGlobalAnonScore = deltaGlobalAnonScore;
				}

				totalVsizeSpecificClient += tx.WalletInputs.Sum(x => x.ScriptPubKey.EstimateInputVsize());

				if (counter % displayProgressEachNRounds == 0)
				{
					WriteLine($"Round N° {counter} - Total vSize: {totalVsizeSpecificClient}");
					DisplayAnonScore(specificClientGlobalAnonScoreAfter, specificClientMaxGlobalAnonScore, maxDeltaGlobalAnonScore, minDeltaGlobalAnonScore);
					DisplayCoins(specificClientSmartCoins);
				}
			}

			WriteLine($"master: {master}, specificClientAmountBtc: {specificClientStartingBalance}, liquidityClue: {LiquidityClueProvider.GetLiquidityClue(maxSuggestedAmount)}, randomSeed: {randomSeed}");
			WriteLine(
				counter >= maxTestRounds
					? $"FAILED specificClientMaxGlobalAnonScore only reached {Math.Round(specificClientMaxGlobalAnonScore * 100, 2)} after {counter} rounds"
					: $"PASSED after {counter} rounds -> Total vSize specificClient: {totalVsizeSpecificClient}");
			DisplayAnonScore(specificClientGlobalAnonScoreAfter, specificClientMaxGlobalAnonScore, maxDeltaGlobalAnonScore, minDeltaGlobalAnonScore);
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

	private void DisplayCoins(IEnumerable<SmartCoin> specificClientSmartCoins)
	{
		WriteLine($"Amount       AnonSet       ");
		var specificClientSmartCoinsOrdered = specificClientSmartCoins.OrderByDescending(x => (int)Math.Round(x.HdPubKey.AnonymitySet));
		foreach (var specificClientSc in specificClientSmartCoinsOrdered)
		{
			WriteLine($"{specificClientSc.Amount} {(int)Math.Round(specificClientSc.HdPubKey.AnonymitySet)}");
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
				listCoinsCurrentOther.Add(BitcoinFactory.CreateSmartCoin(otherHdPub[otherIndex], inputs.RandomElement(rnd)/100000000));
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

	private static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> sc, int anonScoreTarget, RoundParameters roundParams, TestRandomSeed mockSecureRandom, bool master, Money liquidityClue, double p, double q)
	{
		return CoinJoinClient.SelectCoinsForRound(
			coins: sc,
			UtxoSelectionParameters.FromRoundParameters(roundParams),
			consolidationMode: false,
			anonScoreTarget: anonScoreTarget,
			liquidityClue: liquidityClue,
			semiPrivateThreshold: 2,
			rnd: mockSecureRandom,
			master: master,
			p: p,
			q: q);
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
		var specificClientDecomposed = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);
		return specificClientDecomposed.Select(output => (output.Amount, HdPubKey.DefaultHighAnonymitySet)).ToList();
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
