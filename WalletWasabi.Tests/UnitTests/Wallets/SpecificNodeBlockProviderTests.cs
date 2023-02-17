using NBitcoin;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets;

/// <summary>
/// Tests for <see cref="SpecificNodeBlockProvider"/>.
/// </summary>
public class SpecificNodeBlockProviderTests
{
	/// <summary>
	/// No Bitcoin Core node to connect to. No block should be provided by <see cref="SpecificNodeBlockProvider.TryGetBlockAsync(uint256, CancellationToken)"/>.
	/// </summary>
	[Fact]
	public async Task NoRunningPeerAsync()
	{
		Network network = Network.RegTest;
		IPEndPoint bitcoinCoreEndPoint = new(IPAddress.Loopback, port: 44444); // Port is chosen in the way that no Bitcoin Core is expected to listen on it.
		ServiceConfiguration serviceConfiguration = new(bitcoinCoreEndPoint, dustThreshold: Money.Satoshis(1000));

		await using SpecificNodeBlockProvider provider = new(network, serviceConfiguration, torEndPoint: null);

		Block? block = await provider.TryGetBlockAsync(uint256.One, CancellationToken.None);
		Assert.Null(block);
	}
}
