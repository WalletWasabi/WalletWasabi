using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public class BlockHeadersChainBehavior(
	ConcurrentChain blockHeaderChain,
	FilterHeaderChain filterHeaderChain,
	EventBus eventBus,
	TimeProvider? timeProvider = null)
	: ChainBehavior(blockHeaderChain)
{
	private int _lastPublishedHeight;
	private static readonly TimeSpan HeaderProgressTimeout = TimeSpan.FromMinutes(2);
	private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
	private readonly Lock _progressGate = new();
	private long _lastProgressTimestamp;
	private uint256? _lastValidatedTip;
	private IDisposable? _ticks;

	protected override void AttachCore()
	{
		base.AttachCore();
		AttachedNode.StateChanged += AttachedNodeOnStateChanged;
		AttachedNode.MessageReceived += AttachedNodeOnMessageReceived;
		_lastPublishedHeight = Chain.Tip?.Height ?? 0;
		ResetProgress();
		_ticks = eventBus.Subscribe<Tick>(_ => CheckHeaderProgress());
	}

	protected override void DetachCore()
	{
		_ticks?.Dispose();
		_ticks = null;
		AttachedNode.StateChanged -= AttachedNodeOnStateChanged;
		AttachedNode.MessageReceived -= AttachedNodeOnMessageReceived;
		base.DetachCore();
	}

	private void AttachedNodeOnStateChanged(Node node, NodeState oldState)
	{
		if (node.State == NodeState.HandShaked)
		{
			ResetProgress();
			var myBestFilterHeight = filterHeaderChain.ServerTipHeight;
			var theirBestFilterHeight = AttachedNode.PeerVersion.StartHeight;
			if (theirBestFilterHeight > myBestFilterHeight)
			{
				var reportedTip = filterHeaderChain.AdvanceServerTipHeight((uint)theirBestFilterHeight);
				eventBus.Publish(new NetworkTipHeightChanged(reportedTip));
			}
		}
	}

	private void AttachedNodeOnMessageReceived(Node node, IncomingMessage message)
	{
		if (message.Message.Payload is HeadersPayload)
		{
			if (InvalidHeaderReceived)
			{
				RetirePeer(node, invalid: true);
				return;
			}
			ObserveValidatedProgress();
			var currentHeight = Chain.Tip?.Height ?? 0;
			if (currentHeight > _lastPublishedHeight)
			{
				_lastPublishedHeight = currentHeight;
				eventBus.Publish(new BlockHeadersTipChanged((uint)currentHeight));
			}
		}
	}

	private void ResetProgress()
	{
		lock (_progressGate)
		{
			_lastProgressTimestamp = _clock.GetTimestamp();
			_lastValidatedTip = (PendingTip ?? Chain.Tip).HashBlock;
		}
	}

	private void ObserveValidatedProgress()
	{
		lock (_progressGate)
		{
			var tip = (PendingTip ?? Chain.Tip).HashBlock;
			if (tip != _lastValidatedTip)
			{
				_lastValidatedTip = tip;
				_lastProgressTimestamp = _clock.GetTimestamp();
			}
		}
	}

	private void CheckHeaderProgress()
	{
		var node = AttachedNode;
		if (node is null || node.State != NodeState.HandShaked || !CanSync || !AutoSync) { return; }
		if (InvalidHeaderReceived) { RetirePeer(node, invalid: true); return; }
		// A caught-up peer can remain available for block downloads. While the
		// chain is behind its announced height, unanswered getheaders requests
		// must not occupy a connection indefinitely. Existing NBitcoin validation
		// remains responsible for every header and cumulative-work comparison.
		if (node.PeerVersion.StartHeight <= Chain.Tip.Height) { ResetProgress(); return; }
		ObserveValidatedProgress();
		bool stalled;
		lock (_progressGate) { stalled = _clock.GetElapsedTime(_lastProgressTimestamp) >= HeaderProgressTimeout; }
		if (stalled) { RetirePeer(node, invalid: false); }
	}

	private void RetirePeer(Node node, bool invalid)
	{
		if (!node.IsConnected || !ReferenceEquals(AttachedNode, node)) { return; }
		var endpoint = node.Peer.Endpoint;
		var reason = invalid ? "Invalid block header received" : "Block header download stopped making validated progress";
		Logger.LogInfo($"Disconnecting header peer {endpoint}: {reason}");
		node.DisconnectAsync(reason);
		if (invalid) { eventBus.Publish(new MisbehavingNodeDetected(endpoint, node)); }
		else { eventBus.Publish(new NodeTimeoutDownloadingHeaders(endpoint, node)); }
	}

	public override object Clone()
	{
		return new BlockHeadersChainBehavior(Chain, filterHeaderChain, eventBus, _clock);
	}
}
