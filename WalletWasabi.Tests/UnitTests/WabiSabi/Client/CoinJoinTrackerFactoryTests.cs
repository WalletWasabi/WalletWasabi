using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Wallets;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Coordinator;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for <see cref="CoinJoinTrackerFactory"/>.
/// </summary>
public class CoinJoinTrackerFactoryTests
{
	[Fact]
	public void GetOrCreateOutputProvider()
	{
		// The input wallet and the output wallet are the same, so the output provider should be the same as the input provider.
		{
			var wallet = new WalletBuilder().Build();
			Assert.Same(wallet.OutputProvider, CoinJoinTrackerFactory.GetOrCreateOutputProvider(wallet: wallet, outputWallet: wallet));
		}

		// The input wallet and the output wallet are different, so the output provider should be a new instance.
		{
			var inputWallet = new WalletBuilder().Build();
			var outputWallet = new WalletBuilder().Build();

			var actualOutputProvider = CoinJoinTrackerFactory.GetOrCreateOutputProvider(wallet: inputWallet, outputWallet: outputWallet);
			Assert.NotSame(inputWallet.OutputProvider, actualOutputProvider);
			Assert.NotSame(outputWallet.OutputProvider, actualOutputProvider);
			Assert.IsType<PaymentAwareOutputProvider>(actualOutputProvider);
		}
	}

	[Fact]
	public void MixingIntoAnotherWalletPaysOnlyThePaymentsOfTheMixingWallet()
	{
		var wallet = new WalletBuilder().Build();
		var outputWallet = new WalletBuilder().Build();

		var payeeOfWallet = BitcoinFactory.CreateSegwitAddress();
		var payeeOfOutputWallet = BitcoinFactory.CreateSegwitAddress();
		wallet.AddCoinJoinPayment(payeeOfWallet, Money.Coins(0.1m));
		outputWallet.AddCoinJoinPayment(payeeOfOutputWallet, Money.Coins(0.1m));

		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var outputProvider = CoinJoinTrackerFactory.GetOrCreateOutputProvider(wallet: wallet, outputWallet: outputWallet);
		var outputs = outputProvider.GetOutputs(
			roundId: uint256.One,
			roundParameters,
			registeredInputs: [BitcoinFactory.CreateOutPoint()],
			registeredCoinEffectiveValues: [Money.Coins(1m)],
			theirCoinEffectiveValues: [Money.Coins(0.2m), Money.Coins(0.1m), Money.Coins(0.05m)],
			availableVsize: roundParameters.MaxVsizeAllocationPerAlice - Constants.P2wpkhInputVirtualSize,
			arePaymentsAllowed: true).ToArray();

		// The payment of the mixing wallet is made and that wallet knows about it.
		Assert.Contains(outputs, x => x.ScriptPubKey == payeeOfWallet.ScriptPubKey);
		Assert.IsType<InProgressPayment>(Assert.Single(wallet.BatchedPayments.GetPayments()).State);

		// The payment of the output wallet is left alone. Nothing would ever mark it as paid there.
		Assert.DoesNotContain(outputs, x => x.ScriptPubKey == payeeOfOutputWallet.ScriptPubKey);
		Assert.IsType<PendingPayment>(Assert.Single(outputWallet.BatchedPayments.GetPayments()).State);

		// Everything else goes to the output wallet.
		var outputWalletScripts = outputWallet.KeyManager.GetKeys().Select(x => x.GetAssumedScriptPubKey()).ToHashSet();
		Assert.All(outputs.Where(x => x.ScriptPubKey != payeeOfWallet.ScriptPubKey), x => Assert.Contains(x.ScriptPubKey, outputWalletScripts));
	}
}
