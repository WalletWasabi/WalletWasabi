using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class BestEffortEndpointConnectorTests
{
	[Fact]
	public async Task CanConnectWithDifferentModesAsync()
	{
		var connector = new BestEffortEndpointConnector(6);
		var nodeConnectionParameters = new NodeConnectionParameters();
		nodeConnectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(new IPEndPoint(IPAddress.Loopback, 8090), onlyForOnionHosts: true, networkCredential: null, streamIsolation: true));

		using var nodes = new NodesGroup(Network.TestNet, nodeConnectionParameters);

		async Task ConnectAsync(EndPoint endpoint)
		{
			using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			await connector.ConnectSocket(socket, endpoint, nodeConnectionParameters, CancellationToken.None);
		}

		Exception ex;

		// Try to connect to a non-onion address.
		ex = await Assert.ThrowsAsync<SocketException>(
			async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
		Assert.Contains("refused", ex.Message);
		Assert.False(connector.State.AllowOnlyTorEndpoints);

		// Try to connect to an onion address (it has to fail because there is no real socks proxy listening).
		ex = await Assert.ThrowsAnyAsync<SocketException>(
			async () => await ConnectAsync(new DnsEndPoint("nec4kn4ghql7p7an.onion", 180)));
		Assert.Contains("refused", ex.Message);
		Assert.False(connector.State.AllowOnlyTorEndpoints);

		// Simulate we lost connection.
		connector.State.ConnectedNodesCount = 10;
		ex = await Assert.ThrowsAsync<SocketException>(
			async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
		Assert.Contains("refused", ex.Message);
		Assert.True(connector.State.AllowOnlyTorEndpoints);

		ex = await Assert.ThrowsAnyAsync<SocketException>(
			async () => await ConnectAsync(new DnsEndPoint("nec4kn4ghql7p7an.onion", 180)));
		Assert.Contains("refused", ex.Message);
		Assert.True(connector.State.AllowOnlyTorEndpoints);

		// Simulate we lost connection.
		connector.State.ConnectedNodesCount = 0;
		ex = await Assert.ThrowsAsync<SocketException>(
			async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
		Assert.Contains("refused", ex.Message);
		Assert.False(connector.State.AllowOnlyTorEndpoints);

		// Try to connect to an onion address (it has to fail because there is no real socks proxy listening).
		ex = await Assert.ThrowsAnyAsync<SocketException>(
			async () => await ConnectAsync(new DnsEndPoint("nec4kn4ghql7p7an.onion", 180)));
		Assert.Contains("refused", ex.Message);
		Assert.False(connector.State.AllowOnlyTorEndpoints);

		// Enough peers with recent connection.
		connector.State.ConnectedNodesCount = 10;
		ex = await Assert.ThrowsAnyAsync<SocketException>(
			async () => await ConnectAsync(new DnsEndPoint("nec4kn4ghql7p7an.onion", 180)));
		Assert.Contains("refused", ex.Message);
		Assert.True(connector.State.AllowOnlyTorEndpoints);
	}
}
