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
using WalletWasabi.WabiSabi.Coordinator;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for <see cref="CoinSelectors"/>.
/// </summary>
public class CoinSelectorsTests
{
	private const int AnonScoreTarget = 10;

	[Fact]
	public void SelectNothingWithoutPendingPayments()
	{
		var coins = CreateCoins(anonymitySet: 1, 1m, 2m);
		var selector = CoinSelectors.PrivatePaymentCoinSelector(new PaymentBatch(new CoinsRegistry()), minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold);

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
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: AnonScoreTarget, Constants.SemiPrivateThreshold);

		Assert.Equal(2, Select(selector, coins).Count);
	}

	[Fact]
	public void PayWithTheSmallestCoinThatIsEnough()
	{
		var coins = CreateCoins(anonymitySet: AnonScoreTarget + 1, 0.2m, 0.6m, 3m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold);

		Assert.Equal(coins[1], Assert.Single(Select(selector, coins)));
	}

	[Fact]
	public void SelectNothingWhenNoPaymentCanBeMade()
	{
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, semiPrivateThreshold: 0);

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
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.5m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: AnonScoreTarget, Constants.SemiPrivateThreshold);

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
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold);

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
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, semiPrivateThreshold: 0);

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

		Assert.Empty(Select(CoinSelectors.PrivatePaymentCoinSelector(onlyRed, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold), coins));

		var redOrSemiPrivate = new PaymentBatch(new CoinsRegistry());
		AddPaymentWithFailedAttempts(redOrSemiPrivate, Money.Coins(0.5m), [red[1].Outpoint], [red[0].Outpoint, semiPrivate[0].Outpoint]);
		var selection = Select(CoinSelectors.PrivatePaymentCoinSelector(redOrSemiPrivate, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold), coins);

		Assert.Equal(2, selection.Count);
		Assert.Contains(red[1], selection);
		Assert.Contains(semiPrivate[0], selection);
	}

	[Fact]
	public void PayAsManyPaymentsAsPossible()
	{
		var coins = CreateCoins(anonymitySet: AnonScoreTarget + 1, 0.3m, 0.3m, 0.3m);
		var paymentBatch = new PaymentBatch(new CoinsRegistry());
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.8m));
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.2m));
		paymentBatch.AddPayment(GetNewSegwitAddress(), Money.Coins(0.3m));
		var selector = CoinSelectors.PrivatePaymentCoinSelector(paymentBatch, minAnonScoreForPayments: 0, Constants.SemiPrivateThreshold);

		Assert.Equal(2, Select(selector, coins).Count);
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
		selector(coins, CreateUtxoSelectionParameters(), Constants.MaximumNumberOfBitcoinsMoney);

	private static void AddPaymentWithFailedAttempts(PaymentBatch paymentBatch, Money amount, params ImmutableArray<OutPoint>[] failedAttemptsInputs)
	{
		paymentBatch.AddPayment(GetNewSegwitAddress(), amount);
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

	private static BitcoinAddress GetNewSegwitAddress()
	{
		using Key key = new();
		return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
	}

	private static UtxoSelectionParameters CreateUtxoSelectionParameters() =>
		UtxoSelectionParameters.FromRoundParameters(
			WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig()),
			[ScriptType.P2WPKH, ScriptType.Taproot]);
}
