using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Avalonia.Data;
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
using Moq;
using WalletWasabi.WabiSabi.Backend.Rounds;
using DynamicData;
using WalletWasabi.Blockchain.TransactionOutputs;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace WalletWasabi.Tests.UnitTests.Clients;

public class WhaleCoinjoinTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	public WhaleCoinjoinTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Theory]
	[InlineData(1, 0.0005, 0.05, 1234)]
	public void ScoreForWhales(decimal whaleAmountBtc, double otherClientsAmountMin, double otherClientsAmountMax, int randomSeed)
	{
		var nbOtherClients = 1;
		var otherNbInputs = 10;
		var mockSecureRandom = new TestRandomSeed(randomSeed);

		var anonScoreTarget = 100;
		var maxTestRounds = 1000;
		var displayWhaleCoinsEachRounds = int.MaxValue;

		var analyser = new BlockchainAnalyzer();
		var whaleMinInputAnonSet = 1.0;

		KeyManager km = KeyManager.CreateNew(out _, "", Network.RegTest);
		HdPubKey hdPub = BitcoinFactory.CreateHdPubKey(km);
		HdPubKey[] otherHdPub = new HdPubKey[nbOtherClients];
		for (int i = 0; i < nbOtherClients; i++)
		{
			KeyManager kmTmp = KeyManager.CreateNew(out _, "", Network.RegTest);
			otherHdPub[i] = BitcoinFactory.CreateHdPubKey(kmTmp);
		}

		var whaleSmartCoins = new List<SmartCoin> { BitcoinFactory.CreateSmartCoin(hdPub, whaleAmountBtc) };
		var whaleCoins = whaleSmartCoins.Select(sm => sm.Coin);

		var totalVSizeWhale = 0;
		var counter = 0;
		while (whaleMinInputAnonSet < anonScoreTarget || counter >= maxTestRounds)
		{
			counter++;
			var whaleSelectedSmartCoins = SelectCoinsForRound(whaleSmartCoins, anonScoreTarget, mockSecureRandom);
			var whaleSelectedCoins = whaleSelectedSmartCoins.Select(sm => sm.Coin);
			if (!whaleSelectedCoins.Any())
			{
				break;
			}

			whaleSmartCoins.Remove(whaleSelectedSmartCoins);

			var otherSmartCoins = CreateOtherSmartCoins(mockSecureRandom, otherHdPub, otherNbInputs, whaleAmountBtc, otherClientsAmountMin, otherClientsAmountMax);
			var otherSelectedSmartCoins = new List<List<SmartCoin>>();
			var otherSelectedCoins = new List<IEnumerable<Coin>>();
			var otherOutputs = new List<List<(Money, int)>>();

			foreach (var other in otherSmartCoins)
			{
				var tmpSelectedSmartCoins = SelectCoinsForRound(other, anonScoreTarget, mockSecureRandom);
				otherSelectedSmartCoins.Add((tmpSelectedSmartCoins.ToList()));
				var tmpSelectedCoins = tmpSelectedSmartCoins.Select(x => x.Coin);
				otherSelectedCoins.Add(tmpSelectedCoins);
			}

			foreach (var otherSelectedCoin in otherSelectedCoins)
			{
				otherOutputs.Add(DecomposeWithAnonSet(otherSelectedCoin, otherSelectedCoins.SelectMany(x => x).Except(otherSelectedCoin).Concat(whaleSelectedCoins), mockSecureRandom));
			}

			var whaleInputs = whaleSelectedSmartCoins.Select(x => (x.Amount, (int)x.HdPubKey.AnonymitySet));
			var whaleOutputs = DecomposeWithAnonSet(whaleSelectedCoins, otherSelectedCoins.SelectMany(x => x), mockSecureRandom);
			_testOutputHelper.WriteLine($"whaleSelectedCoins: {whaleSelectedCoins.Sum(x => x.Amount)}, otherSmartCoins: {otherSmartCoins.SelectMany(x => x).Sum(x => x.Amount)}, otherSelectedCoins: {otherSelectedCoins.SelectMany(x => x).Sum(x => x.Amount)}, whaleOutputs: {whaleOutputs.Sum(x => x.Item1.Satoshi)}, whaleOutputsCount: {whaleOutputs.Count}, whaleOutputs: {otherOutputs.SelectMany(x => x).Sum(x => x.Item1.Satoshi)}, otherOutputsCount: {otherOutputs.SelectMany(x => x).Count()}, whaleSum: {whaleInputs.Sum(x => x.Item1)} - otherSum: {otherSelectedCoins.SelectMany(x => x).Sum(x => x.Amount)}");
			var tx = BitcoinFactory.CreateSmartTransaction(otherSelectedCoins.SelectMany(x => x).Count(), otherOutputs.SelectMany(x => x).Select(x => x.Item1), whaleInputs, whaleOutputs);
			analyser.Analyze(tx);

			whaleSmartCoins.Add(tx.WalletOutputs.ToList());
			whaleMinInputAnonSet = whaleSmartCoins.Min(x => x.HdPubKey.AnonymitySet);
			totalVSizeWhale += tx.WalletInputs.Sum(x => x.ScriptPubKey.EstimateInputVsize());

			if (counter % displayWhaleCoinsEachRounds == 0)
			{
				DisplayWhaleCoinsAnonSet(counter, totalVSizeWhale, _testOutputHelper, whaleSmartCoins);
			}
		}

		_testOutputHelper.WriteLine($"whaleAmountBtc: {whaleAmountBtc}, otherClientsAmountMin: {otherClientsAmountMin}, otherClientsAmountMax: {otherClientsAmountMax}, randomSeed: {randomSeed}");
		_testOutputHelper.WriteLine(counter >= maxTestRounds
			? $"FAILED whaleMinInputAnonSet only reached {whaleMinInputAnonSet} after {counter} rounds"
			: $"PASSED after {counter} rounds -> Total vSize Whale: {totalVSizeWhale}");
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

	private static void DisplayWhaleCoinsAnonSet(int counter, int totalVSizeWhale, ITestOutputHelper testOutputHelper, IEnumerable<SmartCoin> whaleSmartCoins)
	{
		testOutputHelper.WriteLine($"Total vSize Whale: {totalVSizeWhale}");
		testOutputHelper.WriteLine($"WhaleCoin after {counter} rounds");
		testOutputHelper.WriteLine($"Coin       AnonSet");
		var whaleSmartCoinsOrdered = whaleSmartCoins.OrderByDescending(x => (int)Math.Round(x.HdPubKey.AnonymitySet));
		foreach (var whaleSC in whaleSmartCoinsOrdered)
		{
			testOutputHelper.WriteLine($"{whaleSC.Amount} {(int)Math.Round(whaleSC.HdPubKey.AnonymitySet)}");
		}
	}

	private static List<List<SmartCoin>> CreateOtherSmartCoins(TestRandomSeed rnd, IReadOnlyList<HdPubKey> otherHdPub, int otherNbInputs, decimal whaleAmountBtc, double otherClientsAmountMin, double otherClientsAmountMax)
	{
		var nbOtherClients = otherHdPub.Count;
		var otherSmartCoins = new List<List<SmartCoin>>();
		var otherIndex = 0;
		foreach (var i in DivideEvenly(otherNbInputs, nbOtherClients))
		{
			var listCoinsCurrentOther = new List<SmartCoin>();
			for (var j = 0; j < i; j++)
			{
				listCoinsCurrentOther.Add(BitcoinFactory.CreateSmartCoin(otherHdPub[otherIndex], (whaleAmountBtc * (decimal)GetRandomDouble(rnd, otherClientsAmountMin, otherClientsAmountMax))));
			}

			otherIndex++;
			otherSmartCoins.Add(listCoinsCurrentOther);
		}

		return otherSmartCoins;
	}

	private static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> sc, int anonScoreTarget, TestRandomSeed mockSecureRandom)
	{
		return CoinJoinClient.SelectCoinsForRound(
			coins: sc,
			CreateMultipartyTransactionParameters(),
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

	private static double GetRandomDouble(TestRandomSeed rnd, double min, double max)
	{
		return rnd.GetDouble() * (max - min) + min;
	}

	public static IEnumerable<int> DivideEvenly(int numerator, int denominator)
	{
		int rem;
		int div = Math.DivRem(numerator, denominator, out rem);

		for (int i = 0; i < denominator; i++)
		{
			yield return i < rem ? div + 1 : div;
		}
	}
}
