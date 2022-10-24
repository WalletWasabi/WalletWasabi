using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class WhaleCoinjoinTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	public WhaleCoinjoinTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	public void ScoreForWhales()
	{
		decimal whaleAmountBtc = 1;
		//var othersRatioAmount = 0.01;
		var nbOtherClients = 5;
		var otherNbInputs = 10;

		var analyser = new BlockchainAnalyzer();
		var whaleMinInputAnonSet = 1.0;

		KeyManager km = KeyManager.CreateNew(out _, "testestest", Network.RegTest);
		HdPubKey hdPub = BitcoinFactory.CreateHdPubKey(km);
		HdPubKey[] otherHdPub = new HdPubKey[nbOtherClients];
		for (int i = 0; i < nbOtherClients; i++)
		{
			KeyManager kmTmp = KeyManager.CreateNew(out _, "testestest", Network.RegTest);
			otherHdPub[i] = BitcoinFactory.CreateHdPubKey(kmTmp);
		}

		var whaleSmartCoins = new List<SmartCoin> { BitcoinFactory.CreateSmartCoin(hdPub, whaleAmountBtc) };
		var whaleCoins = whaleSmartCoins.Select(sm => sm.Coin);


		var otherSmartCoins = new List<List<SmartCoin>>();
		var rnd = new Random();
		var otherIndex = 0;
		foreach (int i in DivideEvenly(otherNbInputs, nbOtherClients))
		{
			var listCoinsCurrentOther = new List<SmartCoin>();
			for (int j = 0; j < i; j++)
			{
				listCoinsCurrentOther.Add(BitcoinFactory.CreateSmartCoin(otherHdPub[otherIndex], (whaleAmountBtc * (decimal)GetRandomDouble(rnd))));
			}

			otherIndex++;
			otherSmartCoins.Add(listCoinsCurrentOther);
		}

		_testOutputHelper.WriteLine($"WhaleCoin: {whaleCoins.First().Amount} BTC");
		_testOutputHelper.WriteLine($"Other coins: ");
		foreach (var otherCoin in otherSmartCoins)
		{
			_testOutputHelper.WriteLine($"- {otherCoin.First().Amount}, {otherCoin.Skip(1).First().Amount}");
		}

		var counter = 0;
		while (whaleMinInputAnonSet < 100)
		{
			counter++;
			var whaleSelectedSmartCoins = SelectCoinsForRound(whaleSmartCoins);
			var whaleSelectedCoins = whaleSelectedSmartCoins.Select(sm => sm.Coin);
			whaleSmartCoins.Remove(whaleSelectedSmartCoins);

			var otherSelectedSmartCoins = new List<List<SmartCoin>>();
			var otherSelectedCoins = new List<IEnumerable<Coin>>();
			var otherOutputs = new List<List<(Money, int)>>();

			foreach (var other in otherSmartCoins)
			{
				var tmpSelectedSmartCoins = SelectCoinsForRound(other);
				otherSelectedSmartCoins.Add((tmpSelectedSmartCoins.ToList()));
				var tmpSelectedCoins = tmpSelectedSmartCoins.Select(x => x.Coin);
				otherSelectedCoins.Add(tmpSelectedCoins);
				other.Remove(tmpSelectedSmartCoins);
			}

			foreach (var otherSelectedCoin in otherSelectedCoins)
			{
				otherOutputs.Add(DecomposeWithAnonSet(otherSelectedCoin, otherSelectedCoins.SelectMany(x => x).Except(otherSelectedCoin).Concat(whaleSelectedCoins)));
			}

			var whaleInputs = whaleSelectedSmartCoins.Select(x => (x.Amount, (int)x.HdPubKey.AnonymitySet));
			var whaleOutputs = DecomposeWithAnonSet(whaleSelectedCoins, otherSelectedCoins.SelectMany(x => x));

			var tx = BitcoinFactory.CreateSmartTransaction(otherSelectedCoins.SelectMany(x => x).Count(), otherOutputs.SelectMany(x => x).Select(x => x.Item1), whaleInputs, whaleOutputs);
			analyser.Analyze(tx);
			whaleSmartCoins.Add(tx.WalletOutputs.ToList());
			whaleMinInputAnonSet = whaleSmartCoins.Min(x => x.HdPubKey.AnonymitySet);

			for (int i = 0; i < nbOtherClients; i++)
			{
				var otherInputs = otherSelectedSmartCoins[i].Select(x => (x.Amount, (int)x.HdPubKey.AnonymitySet));
				var txOther = BitcoinFactory.CreateSmartTransaction(otherSelectedCoins.Where((v, index) => index != i).SelectMany(x => x).Concat(whaleSelectedCoins).Count(), otherOutputs.Where((v, index) => index != i).SelectMany(x => x).Select(x => x.Item1).Concat(whaleOutputs.Select(x => x.Item1)), otherInputs, otherOutputs[i]);
				analyser.Analyze(txOther);
				otherSmartCoins[i].Add(txOther.WalletOutputs.ToList());
			}

			if (counter % 2 == 0)
			{
				_testOutputHelper.WriteLine($"WhaleCoin after {counter} rounds");
				_testOutputHelper.WriteLine($"Coin       AnonSet");
				var whaleSmartCoinsOrdered = whaleSmartCoins.OrderByDescending(x => (int)Math.Round(x.HdPubKey.AnonymitySet));
				foreach (var whaleSC in whaleSmartCoinsOrdered)
				{
					_testOutputHelper.WriteLine($"{whaleSC.Amount} {(int)Math.Round(whaleSC.HdPubKey.AnonymitySet)}");
				}
			}
		}

		// TODO: what to do? counters has the nb rounds needed.
		// We never get here
	}

	private static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> sc)
	{
		return CoinJoinClient.SelectCoinsForRound(
			coins: sc,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			anonScoreTarget: 100,
			redCoinIsolation: false,
			liquidityClue: WalletWasabi.Helpers.Constants.MaximumNumberOfBitcoinsMoney,
			SecureRandom.Instance);
	}

	private static List<(Money, int)> DecomposeWithAnonSet(IEnumerable<Coin> ourCoins, IEnumerable<Coin> theirCoins)
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

		var amountDecomposer = new AmountDecomposer(feeRate, allowedOutputAmountRange, Constants.P2wpkhOutputVirtualSize, Constants.P2wpkhInputVirtualSize, availableVsize);
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

	private static double GetRandomDouble(Random rnd)
	{
		double random = rnd.Next(10, 100);
		double ratio = random / 1000;
		return ratio;
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
