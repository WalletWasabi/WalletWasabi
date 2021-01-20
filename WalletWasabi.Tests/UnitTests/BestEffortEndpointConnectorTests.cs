using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class BestEffortEndpointConnectorTests
	{
		[Fact]
		public async Task CanConnectWithDifferentModesAsync()
		{
			var connector = new BestEffortEndpointConnector();
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
			ex = await Assert.ThrowsAsync<InvalidOperationException>(
				async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
			Assert.Equal("The Endpoint connector is configured to allow only Tor endpoints and the '127.0.0.1:180' enpoint is not one", ex.Message);
			Assert.Equal(BestEffortEndpointConnector.ConnectionMode.OnionServiceOnly, connector.State.Mode);

			// Try to connect to an onion address (it has to fail because there is no real socks proxy listening).
			ex = await Assert.ThrowsAnyAsync<SocketException>(
				async () => await ConnectAsync(new DnsEndPoint("nec4kn4ghql7p7an.onion", 80)));
			Assert.Contains("refused", ex.Message);
			Assert.Equal(BestEffortEndpointConnector.ConnectionMode.OnionServiceOnly, connector.State.Mode);

			// Timing out - Mode changes from OnionServiceOnly -> AllowGoingThroughTorExitNodes.
			connector.State.LastModeChangeTime = DateTimeOffset.UtcNow.AddMinutes(-1.5);

			ex = await Assert.ThrowsAnyAsync<SocketException>(
				async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
			Assert.Contains("refused", ex.Message);
			Assert.Equal(BestEffortEndpointConnector.ConnectionMode.AllowGoingThroughTorExitNodes, connector.State.Mode);

			// Simulate there is a connection made (so, it is feasible to have more).
			connector.UpdateConnectedNodesCounter(1);
			connector.State.LastModeChangeTime = DateTimeOffset.UtcNow.AddMinutes(-1.5);

			ex = await Assert.ThrowsAnyAsync<SocketException>(
				async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
			Assert.Contains("refused", ex.Message);
			Assert.Equal(BestEffortEndpointConnector.ConnectionMode.AllowGoingThroughTorExitNodes, connector.State.Mode);

			// Simulate we lost connection.
			connector.State.ConnectedNodesCount = 0;

			ex = await Assert.ThrowsAnyAsync<SocketException>(
				async () => await ConnectAsync(new DnsEndPoint("nec4kn4ghql7p7an.onion", 180)));
			Assert.Contains("refused", ex.Message);
			Assert.Equal(BestEffortEndpointConnector.ConnectionMode.OnionServiceOnly, connector.State.Mode);

			// Simulate there are no connections made.
			connector.State.LastModeChangeTime = DateTimeOffset.UtcNow.AddHours(-1);

			ex = await Assert.ThrowsAnyAsync<SocketException>(
				async () => await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 180)));
			Assert.Contains("refused", ex.Message);
			Assert.Equal(BestEffortEndpointConnector.ConnectionMode.AllowGoingThroughTorExitNodes, connector.State.Mode);
		}
	}
}
