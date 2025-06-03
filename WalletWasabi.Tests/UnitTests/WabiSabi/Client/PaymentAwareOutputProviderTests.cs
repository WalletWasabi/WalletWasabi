using NBitcoin;
using System.Linq;
using WabiSabi.Crypto;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.Coordinator;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class PaymentAwareOutputProviderTests
{
	[Fact]
	public void CreateOutputsForImpossiblePaymentTest()
	{
		var rpc = new MockRpcClient();
		var wallet = new TestWallet("random-wallet", rpc);
		var paymentBatch = new PaymentBatch();

		using Key key = new();
		paymentBatch.AddPayment(
			key.PubKey.GetAddress(ScriptPubKeyType.Segwit, rpc.Network),
			Money.Coins(101.0001m)); // Too big, non-standard payment which cannot be done.

		var roundParameters = WabiSabiFactory.CreateRoundParameters(new Config());
		var registeredCoinsEffectiveValues = new[] { Money.Coins(100m) };
		var theirCoinEffectiveValues = new[] { Money.Coins(0.2m), Money.Coins(0.1m), Money.Coins(0.05m), Money.Coins(0.0025m), Money.Coins(0.0001m) };
		var availableVsize = roundParameters.MaxVsizeAllocationPerAlice - Constants.P2wpkhInputVirtualSize;

		var outputProvider = new PaymentAwareOutputProvider(wallet, paymentBatch);
		var outputs = outputProvider.GetOutputs(
			roundId: uint256.Zero,
			roundParameters,
			registeredCoinsEffectiveValues,
			theirCoinEffectiveValues,
			availableVsize).ToArray();

		var nonAwaredOutputProvider = new OutputProvider(wallet);
		var decomposedOutputs = nonAwaredOutputProvider.GetOutputs(
			uint256.Zero,
			roundParameters,
			registeredCoinsEffectiveValues,
			theirCoinEffectiveValues,
			availableVsize).ToArray();

		decimal ToDecimal(TxOut o) => o.Value.ToDecimal(MoneyUnit.BTC);
		Assert.Equal(outputs.Sum(ToDecimal), decomposedOutputs.Sum(ToDecimal));

		// Make sure this doesn't throw
		var vsizes = Enumerable.Repeat(0L, int.MaxValue).Prepend(availableVsize);
		DependencyGraph.ResolveCredentialDependencies(registeredCoinsEffectiveValues, outputs, roundParameters.MiningFeeRate, vsizes, ProtocolConstants.MaxAmountPerAlice, ProtocolConstants.MaxVsizeCredentialValue);
	}

	[Theory]
	[InlineData("0.00484323")]
	[InlineData("0.007")]
	[InlineData("0.007123")]
	[InlineData("0.00555")]
	[InlineData("0.001, 0.001, 0.001, 0.001, 0.001, 0.001")]
	public void CreateOutputsForPaymentsTest(string testData)
	{
		var payments = testData.Split(",").Select(decimal.Parse).ToArray();
		var rpc = new MockRpcClient();
		var wallet = new TestWallet("random-wallet", rpc);
		var paymentBatch = new PaymentBatch();
		var outputProvider = new PaymentAwareOutputProvider(wallet, paymentBatch);

		var roundParameters = WabiSabiFactory.CreateRoundParameters(new Config());

		var scriptPubKeys = payments.Select(payment =>
		{
			using Key key = new();
			var scriptPubKey = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, rpc.Network);
			paymentBatch.AddPayment(scriptPubKey, Money.Coins(payment));
			return scriptPubKey;
		}).ToArray();

		var registeredCoinsEffectiveValues = new[] { Money.Coins(0.00484323m), Money.Coins(0.003m), Money.Coins(0.00004323m) };
		var totalRegisteredEffectiveValue = registeredCoinsEffectiveValues.Sum().ToDecimal(MoneyUnit.BTC);

		var outputs = outputProvider.GetOutputs(
			roundId: uint256.Zero,
			roundParameters,
			registeredCoinsEffectiveValues,
			new[] { Money.Coins(0.2m), Money.Coins(0.1m), Money.Coins(0.05m), Money.Coins(0.0025m), Money.Coins(0.0001m) },
			int.MaxValue).ToArray();

		Assert.All(payments.Take(4).Zip(scriptPubKeys, outputs, (x, y, z) => (Payment: x, Destination: y, Output: z)), x =>
		{
			Assert.Equal(x.Output.ScriptPubKey, x.Destination.ScriptPubKey);
			Assert.Equal(x.Output.Value, Money.Coins(x.Payment));
		});

		Assert.True(outputs.Length >= 2, $"There were {outputs.Length} outputs."); // The rest was decomposed
		Assert.InRange(outputs.Sum(x => x.Value.ToDecimal(MoneyUnit.BTC)),
			totalRegisteredEffectiveValue - 0.00025m,
			totalRegisteredEffectiveValue); // no money was lost
	}

	[Theory]
	[InlineData(new[] { "0.2", "0.30" }, "0.176", 1_000, 0)] // Not enough money to make any of the payments.
	[InlineData(new[] { "0.1", "0.30" }, "0.176", 1_000, 1)] // It is only possible to make one payment.
	[InlineData(new[] { "0.1", "0.05" }, "0.176", 1_000, 2)] // It is possible to make the two payments.
	[InlineData(new[] { "0.1", "0.05" }, "0.150", 1_000, 1)] // It is only possible to make one payment. Not enough for fees.
	[InlineData(new[] { "0.1", "0.05", "0.025", "0.001", "0.001" }, "0.176", 1_000, 4)] // Four is the maximum number of payments.
	[InlineData(new[] { "0.1", "0.30" }, "0.176", 43 + 31, 1)] // It is only possible to make one payment.
	[InlineData(new[] { "0.1", "0.05" }, "0.176", 43 + 31, 1)] // It is possible to make the two payments.
	[InlineData(new[] { "0.1", "0.05" }, "0.176", 20, 0)] // It is possible to make the two payments.
	[InlineData(new[] { "0.1", "0.05" }, "0.14", 40 + 31, 0)] // Not enough vsize to register the payment and the change.
	public void BestPaymentSetTest(string[] amountsToPay, string availableAmountStr, int availableVsize, int expectedOutputs)
	{
		var roundParameters = WabiSabiFactory.CreateRoundParameters(new Config());
		var paymentBatch = new PaymentBatch();

		var payments = amountsToPay.Select(a => (Destination: GetNewSegwitAddress(), Amount: Money.Coins(decimal.Parse(a))));
		payments.ToList().ForEach(p => paymentBatch.AddPayment(p.Destination, p.Amount));

		var availableMoney = Money.Coins(decimal.Parse(availableAmountStr));
		var paymentSet = paymentBatch.GetBestPaymentSet(availableMoney, availableVsize, roundParameters);

		Assert.True(paymentSet.TotalAmount < availableMoney);
		Assert.True(paymentSet.TotalVSize < availableVsize);
		Assert.Equal(expectedOutputs, paymentSet.Payments.Count());
	}

	private static BitcoinAddress GetNewSegwitAddress()
	{
		using Key key = new();
		return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
	}
}
