using WalletWasabi.Tests.UnitTests.Wallets;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
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
}
