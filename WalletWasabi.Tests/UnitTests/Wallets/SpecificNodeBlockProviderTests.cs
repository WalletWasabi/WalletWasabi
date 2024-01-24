using Moq;
using NBitcoin;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.BlockProvider;
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
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		Network network = Network.RegTest;
		IPEndPoint bitcoinCoreEndPoint = new(IPAddress.Loopback, port: 44444); // Port is chosen in the way that no Bitcoin Core is expected to listen on it.
		ServiceConfiguration serviceConfiguration = new(bitcoinCoreEndPoint, dustThreshold: Money.Satoshis(1000));

		await using SpecificNodeBlockProvider provider = new(network, serviceConfiguration, torEndPoint: null);

		Block? block = await provider.TryGetBlockAsync(uint256.One, testDeadlineCts.Token);
		Assert.Null(block);
	}

	/// <summary>
	/// Simulates successful connection to a block providing node, obtaining a block and then simulating that the node got disconnected.
	/// </summary>
	[Fact]
	public async Task GetValidBlockAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		Network network = Network.Main;
		Block validBlock = Block.Load(Convert.FromHexString(GetRawBlock()), network);
		IPEndPoint bitcoinCoreEndPoint = new(IPAddress.Loopback, port: 44444);
		ServiceConfiguration serviceConfiguration = new(bitcoinCoreEndPoint, dustThreshold: Money.Satoshis(1000));

		using CancellationTokenSource nodeDisconnectedCts = new();
		Mock<ConnectedNode> mockNode = new(MockBehavior.Strict, nodeDisconnectedCts) { CallBase = true };

		// Mock connected node.
		TaskCompletionSource nodeConnectedTcs = new();
		_ = mockNode.Setup(c => c.WaitUntilDisconnectedAsync(It.IsAny<CancellationToken>()))
			.Callback(() => nodeConnectedTcs.SetResult())
			.CallBase();

		_ = mockNode.Setup(c => c.ToString())
			.CallBase();

		_ = mockNode.SetupSequence(c => c.DownloadBlockAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(validBlock)
			.Throws(new OperationCanceledException("Got disconnected"));

		// Mock the provider.
		Mock<SpecificNodeBlockProvider> mockProvider = new(MockBehavior.Strict, network, serviceConfiguration, /* torEndPoint */ null) { CallBase = true };

		_ = mockProvider.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockNode.Object);

		await using SpecificNodeBlockProvider provider = mockProvider.Object;

		// Wait until the node is connected.
		await nodeConnectedTcs.Task.WaitAsync(testDeadlineCts.Token);

		// ... and then get a block.
		Block? actualBlock = await provider.TryGetBlockAsync(uint256.One, testDeadlineCts.Token);
		Assert.NotNull(actualBlock);
		Assert.Same(validBlock, actualBlock);

		// Now the peer should be disconnected but even as such the return value should be null.
		actualBlock = await provider.TryGetBlockAsync(uint256.One, testDeadlineCts.Token);
		Assert.Null(actualBlock);
	}

	private static string GetRawBlock()
	{
		return "01000000a0d4ea3416518af0b238fef847274fc768cd39d0dc44a0ea5ec0c2dd000000007edfbf7974109f1fd628f17dfefd4915f217e0ec06e0c74e45049d36850abca4bc0eb049ffff001d27d0031e0101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0804ffff001d024f02ffffffff0100f2052a010000004341048a5294505f44683bbc2be81e0f6a91ac1a197d6050accac393aad3b86b2398387e34fedf0de5d9f185eb3f2c17f3564b9170b9c262aa3ac91f371279beca0cafac00000000";
	}
}
