using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.Services;
using WalletWasabi.Services.NodesManagement;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class P2pDnsSeedingTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RequestsFilterServingPeersThroughConfiguredResolver(bool testnet)
	{
		var network = testnet ? Network.TestNet4 : Network.Main;
		var resolver = new RecordingResolver(false);
		using var manager = new P2pConnectionManager(network, new EventBus(), resolver, TimeSpan.FromSeconds(10));
		await SeedAsync(manager);
		Assert.NotEmpty(network.DNSSeeds);
		foreach (var seed in network.DNSSeeds)
		{
			Assert.Contains("x49." + seed.Host, resolver.Hosts);
			Assert.Contains(seed.Host, resolver.Hosts);
		}
	}

	[Fact]
	public async Task UnsupportedServiceFilterDoesNotRemoveGeneralSeeds()
	{
		var resolver = new RecordingResolver(true);
		using var manager = new P2pConnectionManager(Network.TestNet4, new EventBus(), resolver, TimeSpan.FromSeconds(10));
		await SeedAsync(manager);
		Assert.Contains(resolver.Hosts, host => host.StartsWith("x49.", StringComparison.Ordinal));
		Assert.All(Network.TestNet4.DNSSeeds, seed => Assert.Contains(seed.Host, resolver.Hosts));
	}

	private static Task SeedAsync(P2pConnectionManager manager) =>
		(Task)typeof(P2pConnectionManager).GetMethod("SeedFromDnsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
			.Invoke(manager, [CancellationToken.None])!;

	private sealed class RecordingResolver(bool unsupportedFilters) : IDnsResolver
	{
		public ConcurrentBag<string> Hosts { get; } = [];
		public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Hosts.Add(host);
			if (unsupportedFilters && host.StartsWith("x49.", StringComparison.Ordinal))
			{
				throw new SocketException((int)SocketError.HostNotFound);
			}
			return Task.FromResult(Array.Empty<IPAddress>());
		}
	}
}
