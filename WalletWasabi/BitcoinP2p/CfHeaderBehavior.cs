using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;


public class CompactFilterHeadersBehavior(ConcurrentChain chain, FilterHeaderChain filterHeaderChain, EventBus eventBus) : NodeBehavior
{
	private const int PageSize = 1000;
	private static readonly TimeSpan TickSyncInterval = TimeSpan.FromMinutes(1);

	// Prevents further sync attempts against this specific peer.
	private volatile bool _invalidHeaderReceived;

	private IDisposable? _tickSubscription;
	private DateTime _lastTickSync = DateTime.MinValue;

	protected override void AttachCore()
	{
		AttachedNode.StateChanged += OnStateChanged;
		AttachedNode.MessageReceived += Intercept;
		_tickSubscription = eventBus.Subscribe<Tick>(OnTick);
	}

	protected override void DetachCore()
	{
		AttachedNode.StateChanged -= OnStateChanged;
		AttachedNode.MessageReceived -= Intercept;
		_tickSubscription?.Dispose();
	}

	public override object Clone()
		=> new CompactFilterHeadersBehavior(chain, filterHeaderChain, eventBus);

	private void OnTick(Tick tick)
	{
		if (tick.DateTime - _lastTickSync >= TickSyncInterval)
		{
			_lastTickSync = tick.DateTime;
			TrySync();
		}
	}

	private void OnStateChanged(Node node, NodeState oldState)
	{
		// Once the handshake completes, kick off the initial sync.
		if (node.State == NodeState.HandShaked)
		{
			TrySync();
		}
	}

	private void Intercept(Node node, IncomingMessage message)
	{
		if (filterHeaderChain.Tip is null)
		{
			return;
		}

		// We only care about cfheaders responses.
		if (message.Message.Payload is not CompactFilterHeadersPayload cfHeaders)
		{
			return;
		}

		// Ignore anything that isn't Basic filters.
		if (cfHeaders.FilterType != FilterType.Basic)
		{
			return;
		}

		// BIP-157: cfheaders message contains FilterHashes (not FilterHeaders).
		// NBitcoin names the property "FilterHeaders" but these are actually filter hashes.
		var filterHashes = cfHeaders.FilterHeaders;
		var batchCount = filterHashes.Count;
		if (batchCount == 0)
		{
			return;
		}

		if (_invalidHeaderReceived)
		{
			return;
		}

		// Resolve the stop block so we know the height range of this batch.
		var stopBlock = chain.GetBlock(cfHeaders.StopHash);
		if (stopBlock == null)
		{
			return; // We don't know this block yet — chain not caught up.
		}

		int startHeight = stopBlock.Height - batchCount + 1;
		if (startHeight < 0)
		{
			InvalidateNode();
			return;
		}

		// Validate that PreviousFilterHeader matches our current tip's filter header.
		// For genesis (height 0), the previous filter header should be uint256.Zero.
		var localPrevFilterHeader = filterHeaderChain.Tip.BlockFilterHeader;
		if (cfHeaders.PreviousFilterHeader != localPrevFilterHeader)
		{
			InvalidateNode();
			return;
		}

		// Compute actual filter headers from filter hashes.
		// BIP-157: filter_header = hash(filter_hash || previous_filter_header)
		var prevFilterHeader = cfHeaders.PreviousFilterHeader;

		for (var i = 0; i < batchCount; i++)
		{
			var block = chain.GetBlock(startHeight + i);
			if (block == null)
			{
				// Gap in our chain — can't validate, abort.
				return;
			}

			var filterHash = filterHashes[i];
			var filterHeader = ComputeFilterHeader(filterHash, prevFilterHeader);

			var smartHeader = new SmartHeader(block.HashBlock, filterHeader, (uint)block.Height, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			try
			{
				filterHeaderChain.AppendTip(smartHeader);
			}
			catch (InvalidOperationException)
			{
				InvalidateNode();
				return;
			}

			prevFilterHeader = filterHeader;
		}

		// If the batch was full (peer may have more), keep paginating.
		if (batchCount == PageSize)
		{
			TrySync();
		}
	}

	private static uint256 ComputeFilterHeader(uint256 filterHash, uint256 prevFilterHeader)
	{
		Span<byte> data = stackalloc byte[64];
		filterHash.ToBytes(data[..32]);
		prevFilterHeader.ToBytes(data[32..]);
		Span<byte> hash = stackalloc byte[32];
		SHA256.HashData(SHA256.HashData(data), hash);
		return new uint256(hash);
	}

	private void InvalidateNode()
	{
		_invalidHeaderReceived = true;
		DetachCore();
	}

	private void TrySync()
	{
		var node = AttachedNode;
		if (node is not {State: NodeState.HandShaked})
		{
			return;
		}

		if (_invalidHeaderReceived)
		{
			return;
		}

		var chainTip = chain.Tip;
		var cfHeaderTip = filterHeaderChain.Tip;
		if (cfHeaderTip is not {} nonNullCfHeaderTip)
		{
			return;
		}
		var startHeight = nonNullCfHeaderTip.Height + 1;

		// Already caught up.
		if (startHeight > chainTip.Height)
		{
			return;
		}

		// Cap the stop block to at most PageSize headers ahead.
		var stopHeight = uint.Min(startHeight + PageSize - 1, (uint)chainTip.Height);
		var stopBlock = chain.GetBlock((int)stopHeight);
		if (stopBlock == null)
		{
			return;
		}

		var payload = new GetCompactFilterHeadersPayload(FilterType.Basic, (uint)startHeight, stopBlock.HashBlock);

		node.SendMessage(payload);
	}
}
