using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class BlockHeaderTransportTests
{
	[Theory]
	[InlineData(1, true, true)]
	[InlineData(0, true, false)]
	[InlineData(1, false, false)]
	public async Task AnUnresponsiveHeaderPeerIsRetiredOnlyWhileDownloadingAsync(int reportedHeight, bool canSync, bool shouldDisconnect)
	{
		var events = new EventBus();
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var accepting = listener.AcceptTcpClientAsync();
		using var node = await Node.ConnectAsync(Network.RegTest, listener.LocalEndpoint);
		using var peer = await accepting;
		var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var filter = node.Filters.Add((_, next) => next(), (_, payload, next) =>
		{
			if (payload is GetHeadersPayload) { requested.TrySetResult(); }
			next();
		});
		var clock = new MonotonicClock();
		var behavior = new BlockHeadersChainBehavior(new ConcurrentChain(Network.RegTest), new FilterHeaderChain(), events, clock) { CanSync = canSync };
		node.Behaviors.Add(behavior);
		typeof(Node).GetField("_PeerVersion", BindingFlags.NonPublic | BindingFlags.Instance)!
			.SetValue(node, new VersionPayload { StartHeight = reportedHeight });
		typeof(Node).GetProperty(nameof(Node.State))!.GetSetMethod(nonPublic: true)!.Invoke(node, [NodeState.HandShaked]);
		try
		{
			if (canSync) { await requested.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
			// The local peer completes TCP writes but never answers getheaders.
			clock.Advance(TimeSpan.FromMinutes(5));
			events.Publish(new Tick(DateTime.UtcNow.AddMinutes(5)));
			Assert.Equal(!shouldDisconnect, node.IsConnected);
		}
		finally { node.DisconnectAsync("Synthetic header timeout test finished"); }
	}

	[Fact]
	public async Task ValidatedHeadersResetTheDeadlineButEmptyRepliesDoNotAsync()
	{
		using var peer = await HeaderPeer.CreateAsync();
		peer.Clock.Advance(TimeSpan.FromSeconds(110));
		var header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = peer.Chain.Tip.HashBlock;
		header.Bits = peer.Chain.Tip.Header.Bits;
		header.BlockTime = peer.Chain.Tip.Header.BlockTime.AddMinutes(1);
		while (!header.CheckProofOfWork()) { header.Nonce++; }
		await peer.SendAsync(new HeadersPayload(header));
		await UntilAsync(() => peer.Chain.Height == 1);
		// Receiving runs on NBitcoin's worker. Wait for the behavior's progress
		// notification before advancing the test clock again.
		await peer.Progress.Task.WaitAsync(TimeSpan.FromSeconds(5));
		peer.Clock.Advance(TimeSpan.FromSeconds(110));
		peer.Events.Publish(new Tick(DateTime.UtcNow));
		Assert.True(peer.Node.IsConnected);
		var emptyReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		peer.Node.MessageReceived += (_, message) =>
		{
			if (message.Message.Payload is HeadersPayload { Headers.Count: 0 }) { emptyReceived.TrySetResult(); }
		};
		await peer.SendAsync(new HeadersPayload());
		await emptyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
		peer.Clock.Advance(TimeSpan.FromSeconds(11));
		peer.Events.Publish(new Tick(DateTime.UtcNow));
		Assert.False(peer.Node.IsConnected);
	}

	[Fact]
	public async Task InvalidProofOfWorkRetiresThePeerWithoutWaitingAsync()
	{
		using var peer = await HeaderPeer.CreateAsync();
		var header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = peer.Chain.Tip.HashBlock;
		header.Bits = peer.Chain.Tip.Header.Bits;
		header.BlockTime = peer.Chain.Tip.Header.BlockTime.AddMinutes(1);
		while (header.CheckProofOfWork()) { header.Nonce++; }
		await peer.SendAsync(new HeadersPayload(header));
		await UntilAsync(() => !peer.Node.IsConnected);
		Assert.True(peer.Behavior.InvalidHeaderReceived);
		Assert.Equal(0, peer.Chain.Height);
	}

	[Fact]
	public async Task WallClockChangesAndDetachedTicksCannotRetireThePeerAsync()
	{
		using var peer = await HeaderPeer.CreateAsync();
		peer.Events.Publish(new Tick(DateTime.UtcNow.AddDays(100)));
		Assert.True(peer.Node.IsConnected);
		peer.Behavior.Detach();
		peer.Clock.Advance(TimeSpan.FromMinutes(5));
		peer.Events.Publish(new Tick(DateTime.UtcNow));
		Assert.True(peer.Node.IsConnected);
	}

	[Fact]
	public async Task ADepartedPeerCannotLeaveAnUnvalidatedTargetBehindAsync()
	{
		using var peer = await HeaderPeer.CreateAsync();
		Assert.Equal(2u, peer.Filters.ServerTipHeight.Height);
		var header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = peer.Chain.Tip.HashBlock;
		header.Bits = peer.Chain.Tip.Header.Bits;
		header.BlockTime = peer.Chain.Tip.Header.BlockTime.AddMinutes(1);
		while (!header.CheckProofOfWork()) { header.Nonce++; }
		await peer.SendAsync(new HeadersPayload(header));
		await peer.Progress.Task.WaitAsync(TimeSpan.FromSeconds(5));
		peer.Clock.Advance(TimeSpan.FromMinutes(3));
		peer.Events.Publish(new Tick(DateTime.UtcNow));
		Assert.False(peer.Node.IsConnected);
		Assert.Equal(1u, peer.Filters.ServerTipHeight.Height);
	}

	[Fact]
	public async Task RemovingAnnouncementsPreservesLiveTargetsAndDoesNotTrustCachedFiltersAsync()
	{
		var filters = new FilterHeaderChain();
		filters.AppendTip(new SmartHeader(Network.RegTest.GetGenesis().GetHash(), uint256.Zero, 0, DateTimeOffset.UtcNow));
		using var first = await HeaderPeer.CreateAsync(filters, 1);
		using var second = await HeaderPeer.CreateAsync(filters, 2);
		first.Behavior.Detach();
		Assert.Equal(2u, filters.ServerTipHeight.Height);
		second.Behavior.Detach();
		Assert.Equal(0u, filters.ServerTipHeight.Height);
		Assert.False(filters.IsSynchronized);
	}

	private static async Task UntilAsync(Func<bool> condition)
	{
		using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition()) { await Task.Delay(10, timeout.Token); }
	}

	[Fact]
	public async Task AuthoritativeRpcReorganizationSurvivesLaterPeerDepartureAsync()
	{
		var filters = new FilterHeaderChain();
		filters.AppendTip(new SmartHeader(Network.RegTest.GetGenesis().GetHash(), uint256.Zero, 0, DateTimeOffset.UtcNow));
		using var peer = await HeaderPeer.CreateAsync(filters, 2);
		Assert.False(filters.IsSynchronized);
		filters.SetServerTipHeight(0); // Authoritative RPC reports a shorter chain.
		peer.Behavior.Detach();
		Assert.Equal(0u, filters.ServerTipHeight.Height);
		Assert.True(filters.IsSynchronized);
	}

	private sealed class HeaderPeer : IDisposable
	{
		public required Node Node { get; init; }
		public required TcpClient Client { get; init; }
		public BlockHeadersChainBehavior Behavior { get; private set; } = null!;
		public ConcurrentChain Chain { get; } = new(Network.RegTest);
		public required FilterHeaderChain Filters { get; init; }
		public EventBus Events { get; } = new();
		public MonotonicClock Clock { get; } = new();
		public TaskCompletionSource Progress { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public static async Task<HeaderPeer> CreateAsync(FilterHeaderChain? filters = null, int reportedHeight = 2)
		{
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var accepting = listener.AcceptTcpClientAsync();
			var node = await Node.ConnectAsync(Network.RegTest, listener.LocalEndpoint);
			var client = await accepting;
			var fixture = new HeaderPeer { Node = node, Client = client, Filters = filters ?? new FilterHeaderChain() };
			fixture.Behavior = new(fixture.Chain, fixture.Filters, fixture.Events, fixture.Clock);
			fixture.Events.Subscribe<BlockHeadersTipChanged>(_ => fixture.Progress.TrySetResult());
			node.Behaviors.Add(fixture.Behavior);
			typeof(Node).GetField("_PeerVersion", BindingFlags.NonPublic | BindingFlags.Instance)!
				.SetValue(node, new VersionPayload { StartHeight = reportedHeight });
			typeof(Node).GetProperty(nameof(Node.State))!.GetSetMethod(nonPublic: true)!.Invoke(node, [NodeState.HandShaked]);
			return fixture;
		}
		public Task SendAsync(Payload payload) => Client.GetStream().WriteAsync(new Message { Magic = Network.RegTest.Magic, Payload = payload }.ToBytes()).AsTask();
		public void Dispose() { Node.DisconnectAsync("Synthetic header transport test finished"); Node.Dispose(); Client.Dispose(); }
	}

	private sealed class MonotonicClock : TimeProvider
	{
		private long _timestamp;
		public override long TimestampFrequency => TimeSpan.TicksPerSecond;
		public override long GetTimestamp() => _timestamp;
		public void Advance(TimeSpan time) => _timestamp += time.Ticks;
	}
}
