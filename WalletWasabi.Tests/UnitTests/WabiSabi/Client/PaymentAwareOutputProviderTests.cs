using NBitcoin;
using System.Linq;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Client.Batching;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class PaymentAwareOutputProviderTests
{
	[Fact]
	public void CreateOutputsForPaymentsTest()
	{
		var rpc = new MockRpcClient();
		var wallet = new TestWallet("random-wallet", rpc);
		var paymentBatch = new PaymentBatch();
		var outputProvider = new PaymentAwareOutputProvider(wallet, paymentBatch);

		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		using Key key = new();
		paymentBatch.AddPayment(
			key.PubKey.GetAddress(ScriptPubKeyType.Segwit, rpc.Network),
			Money.Coins(0.00005432m));

		var outputs =
			outputProvider.GetOutputs(
			uint256.Zero,
			roundParameters,
			new[] { Money.Coins(0.00484323m), Money.Coins(0.003m), Money.Coins(0.00004323m) },
			new[] { Money.Coins(0.2m), Money.Coins(0.1m), Money.Coins(0.05m), Money.Coins(0.0025m), Money.Coins(0.0001m) },
			int.MaxValue).ToArray();

		Assert.Equal(outputs[0].ScriptPubKey, key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
		Assert.Equal(outputs[0].Value, Money.Coins(0.00005432m));

		Assert.True(outputs.Length > 2, $"There were {outputs.Length} outputs."); // The rest was decomposed
		Assert.InRange(outputs.Sum(x => x.Value.ToDecimal(MoneyUnit.BTC)), 0.007600m, 0.007800m); // no money was lost
	}
}
