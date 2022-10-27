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

	public WhaleCoinjoinTests(ITestOutputHelper testOutputHelper)
	{
		TestOutputHelper = testOutputHelper;
		var outputFolder = Directory.CreateDirectory(Common.GetWorkDir(nameof(WhaleCoinjoinTests), "Output"));
		var date = DateTime.Now.ToString("Md_HHmmss");
		OutputFilePath = Path.Combine(outputFolder.FullName, $"Outputs{date}.txt");
	}

	[Theory]
	[InlineData(1, 0.0005, 0.05, 1234)]
	[InlineData(1, 0.0005, 0.05, 6789)]
	[InlineData(1, 0.001, 0.1, 1234)]
	[InlineData(1, 0.0005, 0.2, 1234)]
	[InlineData(1, 0.0005, 0.01, 1234)]
	public void ScoreForWhales(double whaleAmountBtc, double otherClientsAmountMin, double otherClientsAmountMax, int randomSeed)
	{
		var nbOtherClients = 30;
		var otherNbInputsPerClient = 5;
		var mockSecureRandom = new TestRandomSeed(randomSeed);

		var anonScoreTarget = 100;
		var maxTestRounds = 1000;
		var displayWhaleCoinsEachRounds = int.MaxValue;

		var analyser = new BlockchainAnalyzer();
		var whaleMinInputAnonSet = 1.0;

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
		var counter = 0;
		while (whaleMinInputAnonSet < anonScoreTarget && counter <= maxTestRounds)
		{
			counter++;
			var whaleSelectedSmartCoins = SelectCoinsForRound(whaleSmartCoins, anonScoreTarget, roundParams, mockSecureRandom);
			var whaleSelectedCoins = whaleSelectedSmartCoins.Select(sm => sm.Coin);
			if (!whaleSelectedCoins.Any())
			{
				break;
			}

			whaleSmartCoins.Remove(whaleSelectedSmartCoins);

			var otherSmartCoins = CreateOtherSmartCoins(mockSecureRandom, otherHdPub, otherNbInputsPerClient, otherClientsAmountMin, otherClientsAmountMax);
			var otherSelectedSmartCoins = new List<List<SmartCoin>>();
			var otherSelectedCoins = new List<IEnumerable<Coin>>();
			var otherOutputs = new List<List<(Money, int)>>();

			foreach (var other in otherSmartCoins)
			{
				var tmpSelectedSmartCoins = SelectCoinsForRound(other, anonScoreTarget, roundParams, mockSecureRandom);
				otherSelectedSmartCoins.Add((tmpSelectedSmartCoins.ToList()));
				otherSelectedCoins.Add(tmpSelectedSmartCoins.Select(x => x.Coin));
			}

			foreach (var otherSelectedCoin in otherSelectedCoins)
			{
				otherOutputs.Add(DecomposeWithAnonSet(otherSelectedCoin, otherSelectedCoins.SelectMany(x => x).Except(otherSelectedCoin).Concat(whaleSelectedCoins), mockSecureRandom));
			}

			var whaleInputs = whaleSelectedSmartCoins.Select(x => (x.Amount, (int)x.HdPubKey.AnonymitySet));
			var whaleOutputs = DecomposeWithAnonSet(whaleSelectedCoins, otherSelectedCoins.SelectMany(x => x), mockSecureRandom);
			var tx = BitcoinFactory.CreateSmartTransaction(otherSelectedCoins.SelectMany(x => x).Count(), otherOutputs.SelectMany(x => x).Select(x => x.Item1), whaleInputs, whaleOutputs);
			analyser.Analyze(tx);

			whaleSmartCoins.Add(tx.WalletOutputs.ToList());
			whaleMinInputAnonSet = whaleSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
			totalVSizeWhale += tx.WalletInputs.Sum(x => x.ScriptPubKey.EstimateInputVsize());

			if (counter % displayWhaleCoinsEachRounds == 0)
			{
				DisplayWhaleCoinsAnonSet(counter, totalVSizeWhale, whaleSmartCoins);
			}
		}

		WriteLine($"whaleAmountBtc: {whaleAmountBtc}, otherClientsAmountMin: {otherClientsAmountMin}, otherClientsAmountMax: {otherClientsAmountMax}, randomSeed: {randomSeed}");
		WriteLine(counter >= maxTestRounds
			? $"FAILED whaleMinInputAnonSet only reached {whaleMinInputAnonSet} after {counter} rounds"
			: $"PASSED after {counter} rounds -> Total vSize Whale: {totalVSizeWhale}");
	}

	public class TestRandomSeed : SecureRandom
	{
		private readonly Random _seededInstance;
		public int CurrentDepth = 0;

		public TestRandomSeed(int seed)
		{
			_seededInstance = new Random(seed);
		}

		public override int GetInt(int fromInclusive, int toExclusive)
		{
			CurrentDepth++;
			return _seededInstance.Next(fromInclusive, toExclusive);
		}

		public double GetDouble()
		{
			CurrentDepth++;
			return _seededInstance.NextDouble();
		}
	}

	private void WriteLine(string content)
	{
		TestOutputHelper.WriteLine(content);
		using StreamWriter sw = new(OutputFilePath, append: true);
		sw.WriteLine(content);
	}

	private void DisplayWhaleCoinsAnonSet(int counter, int totalVSizeWhale, IEnumerable<SmartCoin> whaleSmartCoins)
	{
		WriteLine($"Total vSize Whale: {totalVSizeWhale}");
		WriteLine($"WhaleCoin after {counter} rounds");
		WriteLine($"Coin       AnonSet");
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

	private static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> sc, int anonScoreTarget, RoundParameters roundParams, TestRandomSeed mockSecureRandom)
	{
		return CoinJoinClient.SelectCoinsForRound(
			coins: sc,
			roundParams,
			consolidationMode: false,
			anonScoreTarget: anonScoreTarget,
			redCoinIsolation: false,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
			rnd: mockSecureRandom
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
