using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class CompactFilterTransportTests
{
	[Theory]
	[InlineData(true, true, false)]
	[InlineData(false, true, false)]
	[InlineData(true, false, false)]
	[InlineData(false, false, false)]
	[InlineData(true, false, true)]
	[InlineData(false, false, true)]
	public async Task AStalledSendCannotBlockAssignmentCleanupAsync(bool requestHeaders, bool timeout, bool failQueue)
	{
		var network = Network.RegTest;
		var blockHeaders = new ConcurrentChain(network);
		var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = blockHeaders.Tip.HashBlock;
		header.Nonce = 1;
		blockHeaders.SetTip(new ChainedBlock(header, header.GetHash(), blockHeaders.Tip));
		var filterHeaders = new FilterHeaderChain();
		var genesis = FilterCheckpoints.GetWasabiGenesisFilter(network);
		filterHeaders.AppendTip(genesis.Header);
		if (!requestHeaders)
		{
			var filter = new GolombRiceFilterBuilder().SetKey(header.GetHash())
				.AddEntries([Script.FromHex("00141111111111111111111111111111111111111111").ToBytes()]).Build();
			filterHeaders.AppendTip(new SmartHeader(header.GetHash(), filter.GetHeader(genesis.Header.BlockFilterHeader), 1, header.BlockTime));
		}
		var state = new FilterSynchronizationState(blockHeaders, filterHeaders, tipHeight: 0);
		var events = new EventBus();
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var accepting = listener.AcceptTcpClientAsync();
		using var node = await Node.ConnectAsync(network, listener.LocalEndpoint);
		using var peer = await accepting;
		var pendingSends = new ConcurrentQueue<Action>();
		var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseSends = 0;
		using var sendFilter = node.Filters.Add((_, next) => next(), (_, payload, next) =>
		{
			if (Volatile.Read(ref releaseSends) == 0 && payload is GetCompactFilterHeadersPayload or GetCompactFiltersPayload)
			{
				if (failQueue)
				{
					requested.TrySetResult();
					throw new IOException("Synthetic outbound queue failure");
				}
				pendingSends.Enqueue(next);
				requested.TrySetResult();
			}
			else { next(); }
		});
		var behavior = new CompactFilterBehavior(state, blockHeaders, events);
		node.Behaviors.Add(behavior);
		// A local transport fault, without an external peer or a protocol handshake.
		// Hold NBitcoin's send completion after advertising compact-filter support.
		typeof(Node).GetField("_PeerVersion", BindingFlags.NonPublic | BindingFlags.Instance)!
			.SetValue(node, new VersionPayload { Services = NodeServices.NODE_COMPACT_FILTERS });
		var handshake = Task.Run(() => typeof(Node).GetProperty(nameof(Node.State))!
			.GetSetMethod(nonPublic: true)!.Invoke(node, [NodeState.HandShaked]));
		Task? tick = null;
		try
		{
			await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
			tick = failQueue ? handshake : Task.Run(() =>
			{
				if (timeout) { events.Publish(new Tick(DateTime.UtcNow.AddMinutes(2))); }
				else { behavior.Detach(); }
			});
			await Task.WhenAny(tick, Task.Delay(TimeSpan.FromSeconds(2)));
			Assert.True(tick.IsCompleted, "A pending socket send must not hold the assignment lock or block the global ticker.");
			await tick;
			if (timeout || failQueue) { Assert.False(node.IsConnected); }
			else { Assert.Null(behavior.AttachedNode); }
			var reassigned = requestHeaders
				? state.TryAssignHeaderRange(network, out var assignment)
				: state.TryAssignFilterRange(out assignment);
			Assert.True(reassigned);
			Assert.Equal(1u, assignment!.StartHeight);
		}
		finally
		{
			// Release the fault even when the regression assertion fails. Do not leave
			// the old synchronous send or a Node cleanup thread blocked in the suite.
			Volatile.Write(ref releaseSends, 1);
			while (pendingSends.TryDequeue(out var next)) { next(); }
			await handshake.WaitAsync(TimeSpan.FromSeconds(5));
			if (tick is not null) { await tick.WaitAsync(TimeSpan.FromSeconds(5)); }
			node.DisconnectAsync("Synthetic transport test finished");
		}
	}
}
