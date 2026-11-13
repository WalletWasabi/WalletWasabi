using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Coordinator;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinTrackerFactoryTests
{
	[Fact]
	public void MixingIntoAnotherWalletPaysOnlyThePaymentsOfTheMixingWallet()
	{
		var walletFactory = WalletWasabi.Wallets.Wallet.CreateFactory(Network.Main, null!, null!, null!, null!, new ServiceConfiguration(Money.Satoshis(1000)), null!, new EventBus(), null!);
		var wallet = walletFactory(ServiceFactory.CreateKeyManager(""));
		var outputWallet = walletFactory(ServiceFactory.CreateKeyManager(""));

		var payeeOfWallet = BitcoinFactory.CreateSegwitAddress();
		var payeeOfOutputWallet = BitcoinFactory.CreateSegwitAddress();
		wallet.AddCoinJoinPayment(payeeOfWallet, Money.Coins(0.1m));
		outputWallet.AddCoinJoinPayment(payeeOfOutputWallet, Money.Coins(0.1m));

		var roundParameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig());
		var outputProvider = CoinJoinTrackerFactory.CreateOutputProvider(wallet, outputWallet);
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

	[Fact]
	public void MixingIntoTheSameWalletUsesTheOutputProviderOfTheWallet()
	{
		var walletFactory = WalletWasabi.Wallets.Wallet.CreateFactory(Network.Main, null!, null!, null!, null!, new ServiceConfiguration(Money.Satoshis(1000)), null!, new EventBus(), null!);
		var wallet = walletFactory(ServiceFactory.CreateKeyManager(""));

		Assert.Same(wallet.OutputProvider, CoinJoinTrackerFactory.CreateOutputProvider(wallet, wallet));
	}
}
