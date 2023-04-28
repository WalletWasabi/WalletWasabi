using NBitcoin;
using System.Linq;
using SkiaSharp;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class PaymentAwareOutputProviderTests
{
	[Fact]
	public void CreateOutputsForPaymentsTest()
	{
		var rpc = new MockRpcClient();
		var wallet = new TestWallet("random-wallet", rpc);
		var outputProvider = new PaymentAwareOutputProvider(wallet);

		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		using Key key = new();
		outputProvider.AddPendingPayment(new PendingPayment(key.PubKey.GetAddress(ScriptPubKeyType.Segwit, rpc.Network), Money.Coins(1.2m)));

		var outputs =
			outputProvider.GetOutputs(
			roundParameters,
			new[] { Money.Coins(1m), Money.Coins(1m) },
			new[] { Money.Coins(2m), Money.Coins(1m), Money.Coins(0.5m), Money.Coins(0.25m), Money.Coins(0.1m) },
			int.MaxValue).ToArray();

		Assert.Equal(outputs[0].ScriptPubKey, key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
		Assert.Equal(outputs[0].Value, Money.Coins(1.2m));

		Assert.True(outputs.Length > 2); // the rest was decomposed
		Assert.InRange(outputs.Sum(x => x.Value.ToDecimal(MoneyUnit.BTC)), 1.999m, 2m); // no money was lost
	}
}
