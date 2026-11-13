using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinSelectorsTests
{
	private const int AnonScoreTarget = 10;
	private static readonly CoinJoinCoinSelectorRandomnessGenerator Generator = new(CoinJoinCoinSelector.MaxInputsRegistrableByWallet, RandomExtensions.CreateSeeded(seed: 0));

	[Fact]
	public void SelectNothingWithoutPendingPayments()
	{
		var coins = CreateCoins(anonymitySet: 1, 1m, 2m);
		var selector = CoinSelectors.PrivatePaymentCoinSelector(new PaymentBatch(new CoinsRegistry()), minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold, Generator);

		Assert.Empty(Select(selector, coins));
	}

	/// <summary>
	/// A wallet with nothing left to mix still pays with its private coins (issue #15015).
	/// </summary>
	[Fact]
	public void PayWithPrivateCoinsWhenThereIsNothingToMix()
	{
		var coins = CreateCoins(anonymitySet: AnonScoreTarget + 1, 0.3m, 0.3m, 0.3m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: AnonScoreTarget, Constants.SemiPrivateThreshold, Generator);

		Assert.InRange(Select(selector, coins).Count, 2, 3);
	}

	/// <summary>
	/// The coins are drawn at random, as many as a coinjoin would mix, so the payment is not tied to the coins that could pay it alone.
	/// </summary>
	[Fact]
	public void PayWithRandomCoins()
	{
		var coins = CreateCoins(anonymitySet: AnonScoreTarget + 1, Enumerable.Repeat(0.1m, 15).ToArray());
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.05m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold, Generator);

		var selections = Enumerable.Range(0, 20).Select(_ => Select(selector, coins)).ToArray();

		Assert.All(selections, selection => Assert.InRange(selection.Count, 1, CoinJoinCoinSelector.MaxInputsRegistrableByWallet));
		Assert.Equal(coins.Length, selections.SelectMany(selection => selection).Distinct().Count());
	}

	/// <summary>
	/// What is left after the payment must still make the smallest denomination, otherwise it cannot be registered (issue #15088).
	/// </summary>
	[Fact]
	public void LeaveMoreThanTheSmallestDenominationBehind()
	{
		var parameters = WabiSabiFactory.CreateUtxoSelectionParameters();
		var payment = Money.Coins(0.02m);
		var fees = parameters.MiningFeeRate.GetFee(Constants.P2wpkhInputVirtualSize + Constants.P2wpkhOutputVirtualSize);
		var tightCoin = CreateCoins(anonymitySet: 1, (payment + fees + parameters.MinAllowedOutputAmount).ToDecimal(MoneyUnit.BTC));
		var smallCoin = CreateCoins(anonymitySet: 1, 0.001m);
		var comfortableCoin = CreateCoins(anonymitySet: 1, 0.03m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), payment);
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, semiPrivateThreshold: 0, Generator);

		Assert.Contains(comfortableCoin[0], Select(selector, [.. tightCoin, .. comfortableCoin]));
		Assert.Equal(2, Select(selector, [.. tightCoin, .. smallCoin]).Count);
		Assert.Empty(Select(selector, tightCoin));
	}

	/// <summary>
	/// A payment that needs all the large coins is almost never funded by random draws, so the largest coins fund it as the last attempt.
	/// </summary>
	[Fact]
	public void PayWithTheLargestCoinsWhenRandomCoinsCannot()
	{
		var large = CreateCoins(anonymitySet: 1, Enumerable.Repeat(0.1m, 10).ToArray());
		var small = CreateCoins(anonymitySet: 1, Enumerable.Repeat(0.001m, 50).ToArray());
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.95m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, semiPrivateThreshold: 0, Generator);

		var selection = Select(selector, [.. large, .. small]);

		Assert.Equal(large.Length, selection.Count);
		Assert.All(large, coin => Assert.Contains(coin, selection));
	}

	[Fact]
	public void SelectNothingWhenNoPaymentCanBeMade()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, semiPrivateThreshold: 0, Generator);

		var notEnoughMoney = CreateCoins(anonymitySet: 1, 0.2m, 0.2m);
		var tooManyCoins = CreateCoins(anonymitySet: 1, Enumerable.Repeat(0.01m, 60).ToArray());

		Assert.Empty(Select(selector, notEnoughMoney));
		Assert.Empty(Select(selector, tooManyCoins));
	}

	[Fact]
	public void PayOnlyWithPrivateCoinsWhenOnlyPrivateFundsAreAllowed()
	{
		var red = CreateCoins(anonymitySet: 1, 5m);
		var semiPrivate = CreateCoins(anonymitySet: AnonScoreTarget - 1, 5m);
		var privateCoins = CreateCoins(anonymitySet: AnonScoreTarget, 0.4m, 0.4m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: AnonScoreTarget, Constants.SemiPrivateThreshold, Generator);

		var selection = Select(selector, [.. red, .. semiPrivate, .. privateCoins]);

		Assert.Equal(2, selection.Count);
		Assert.All(privateCoins, coin => Assert.Contains(coin, selection));
		Assert.Empty(Select(selector, [.. red, .. semiPrivate, privateCoins[0]]));
	}

	/// <summary>
	/// Red coins are isolated from each other, so a wallet of fresh deposits spends only one of them per round.
	/// To retry a payment it must be the one the failed attempt spent, not a random one.
	/// </summary>
	[Fact]
	public void RetryWithTheRedCoinOfTheFailedAttempt()
	{
		var coins = CreateCoins(anonymitySet: 1, 1m, 1.1m, 1.2m, 1.3m, 1.4m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		AddPaymentWithFailedAttempts(paymentBatch, Money.Coins(0.5m), [coins[1].Outpoint, BitcoinFactory.CreateOutPoint()]);
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold, Generator);

		Assert.Equal(coins[1], Assert.Single(Select(selector, coins)));
	}

	[Fact]
	public void RetrySpendsAnInputOfEveryFailedAttempt()
	{
		var coins = CreateCoins(anonymitySet: 1, 0.1m, 0.2m, 3m, 4m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		AddPaymentWithFailedAttempts(
			paymentBatch,
			Money.Coins(0.5m),
			[coins[0].Outpoint, BitcoinFactory.CreateOutPoint()],
			[coins[1].Outpoint, BitcoinFactory.CreateOutPoint()]);
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, semiPrivateThreshold: 0, Generator);

		var selection = Select(selector, coins);

		Assert.Contains(coins[0], selection);
		Assert.Contains(coins[1], selection);
		Assert.True(paymentBatch.CanBeRetriedWith(paymentBatch.GetPendingPayments().Single(), selection.Select(x => x.Outpoint).ToHashSet()));
	}

	/// <summary>
	/// Two failed attempts that spent different red coins cannot both be spent without merging red coins,
	/// so the payment waits. Spending a non-red input of an attempt instead keeps the payment possible.
	/// </summary>
	[Fact]
	public void DoNotSelectASecondRedCoinToRetry()
	{
		var red = CreateCoins(anonymitySet: 1, 2m, 1m);
		var semiPrivate = CreateCoins(anonymitySet: AnonScoreTarget - 1, 1m);
		SmartCoin[] coins = [.. red, .. semiPrivate];

		var onlyRed = new PaymentBatch(new CoinsRegistry());
		AddPaymentWithFailedAttempts(onlyRed, Money.Coins(0.5m), [red[0].Outpoint], [red[1].Outpoint]);

		Assert.Empty(Select(CoinSelectors.PrivatePaymentCoinSelector(onlyRed, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold, Generator), coins));

		var redOrSemiPrivate = new PaymentBatch(new CoinsRegistry());
		AddPaymentWithFailedAttempts(redOrSemiPrivate, Money.Coins(0.5m), [red[1].Outpoint], [red[0].Outpoint, semiPrivate[0].Outpoint]);
		var selection = Select(CoinSelectors.PrivatePaymentCoinSelector(redOrSemiPrivate, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold, Generator), coins);

		Assert.Equal(2, selection.Count);
		Assert.Contains(red[1], selection);
		Assert.Contains(semiPrivate[0], selection);
	}

	[Fact]
	public void PayAsManyPaymentsAsPossible()
	{
		var coins = CreateCoins(anonymitySet: AnonScoreTarget + 1, 0.3m, 0.3m, 0.3m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.8m));
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.2m));
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), Money.Coins(0.3m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold, Generator);

		Assert.InRange(Select(selector, coins).Count, 2, 3);
	}

	[Fact]
	public void ComposedCoinSelectorTakesTheFirstSelection()
	{
		var coins = CreateCoins(anonymitySet: 1, 1m, 2m);
		CoinSelector selectNothing = (_, _, _) => [];
		CoinSelector selectFirst = (candidates, _, _) => [candidates[0]];
		CoinSelector fail = (_, _, _) => throw new InvalidOperationException("Only the first selection counts.");

		Assert.Equal(coins[0], Assert.Single(Select(CoinSelectors.ComposedCoinSelector([selectNothing, selectFirst, fail]), coins)));
		Assert.Empty(Select(CoinSelectors.ComposedCoinSelector([selectNothing, selectNothing]), coins));
	}

	private static ImmutableList<SmartCoin> Select(CoinSelector selector, SmartCoin[] coins) =>
		selector(coins, WabiSabiFactory.CreateUtxoSelectionParameters(), Constants.MaximumNumberOfBitcoinsMoney);

	private static void AddPaymentWithFailedAttempts(PaymentBatch paymentBatch, Money amount, params ImmutableArray<OutPoint>[] failedAttemptsInputs)
	{
		paymentBatch.AddPayment(BitcoinFactory.CreateSegwitAddress(), amount);
		foreach (var inputs in failedAttemptsInputs)
		{
			paymentBatch.MovePaymentsToInProgress(paymentBatch.GetPendingPayments(), uint256.One);
			paymentBatch.MovePaymentsToSigned(RandomUtils.GetUInt256(), inputs);
			paymentBatch.MoveSignedPaymentsToPending();
		}
	}

	private static SmartCoin[] CreateCoins(int anonymitySet, params decimal[] amounts)
	{
		var keyManager = KeyManager.CreateNew(out _, "", Network.Main);
		return amounts
			.Select(amount => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(keyManager), Money.Coins(amount), anonymitySet: anonymitySet))
			.ToArray();
	}
}
