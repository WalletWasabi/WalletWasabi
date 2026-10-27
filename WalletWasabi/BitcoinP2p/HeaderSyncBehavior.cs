using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

/// <summary>
/// Keeps the block header chain in sync with the peer. Unlike NBitcoin's ChainBehavior it works on a
/// chain whose first block is a checkpoint (not genesis), so the locator stops at the chain base.
/// </summary>
public class HeaderSyncBehavior(
	ConcurrentChain blockHeaderChain,
	FilterHeaderChain filterHeaderChain,
	EventBus eventBus)
	: NodeBehavior
{
	private const int MaxHeadersPerMessage = 2000;

	private ChainedBlock? _peerTip;
	private bool _invalidHeaderReceived;

	protected override void AttachCore()
	{
		AttachedNode.StateChanged += OnStateChanged;
		AttachedNode.MessageReceived += OnMessageReceived;
	}

	protected override void DetachCore()
	{
		AttachedNode.StateChanged -= OnStateChanged;
		AttachedNode.MessageReceived -= OnMessageReceived;
	}

	public override object Clone() =>
		new HeaderSyncBehavior(blockHeaderChain, filterHeaderChain, eventBus);

	private void OnStateChanged(Node node, NodeState oldState)
	{
		if (node.State == NodeState.HandShaked)
		{
			RequestHeaders(node);
		}
	}

	private void OnMessageReceived(Node node, IncomingMessage message)
	{
		switch (message.Message.Payload)
		{
			case InvPayload inv when inv.Inventory.Any(i => i.Type.HasFlag(InventoryType.MSG_BLOCK) && blockHeaderChain.GetBlock(i.Hash) is null):
				RequestHeaders(node);
				break;
			case HeadersPayload headers:
				ProcessHeaders(node, headers);
				break;
		}
	}

	private void RequestHeaders(Node node)
	{
		if (node.State != NodeState.HandShaked || _invalidHeaderReceived || blockHeaderChain.Tip is not { } tip)
		{
			return;
		}


		if (_peerTip is null || blockHeaderChain.GetBlock(_peerTip.HashBlock) is not null)
		{
			_peerTip = tip;
		}

		node.SendMessageAsync(new GetHeadersPayload { BlockLocators = GetLocator(_peerTip) });
	}

	private void ProcessHeaders(Node node, HeadersPayload headers)
	{
		if (_invalidHeaderReceived || blockHeaderChain.Tip is null)
		{
			return;
		}

		var start = _peerTip ?? blockHeaderChain.Tip;
		var cursor = start;
		foreach (var header in headers.Headers)
		{
			var previous = cursor.FindAncestorOrSelf(header.HashPrevBlock) ?? blockHeaderChain.GetBlock(header.HashPrevBlock);
			if (previous is null)
			{
				break;
			}

			var hash = header.GetHash();
			if (blockHeaderChain.GetBlock(hash) is { } known)
			{
				cursor = known;
				continue;
			}

			var candidate = new ChainedBlock(header, hash, previous);
			if (!candidate.Validate(node.Network))
			{
				_invalidHeaderReceived = true;
				Logger.LogWarning($"Peer {node.Peer.Endpoint} sent an invalid block header at height {candidate.Height}. Ignoring its headers from now on.");
				return;
			}

			cursor = candidate;
		}

		var tip = blockHeaderChain.Tip;
		if (cursor.GetChainWork(cacheResult: true) > tip.GetChainWork(cacheResult: true))
		{
			blockHeaderChain.SetTip(cursor);
			PublishTip((uint)cursor.Height);
		}

		_peerTip = blockHeaderChain.GetBlock(cursor.HashBlock) ?? cursor;

		if (headers.Headers.Count >= MaxHeadersPerMessage && cursor.HashBlock != start.HashBlock)
		{
			RequestHeaders(node);
		}
	}

	private void PublishTip(uint height)
	{
		eventBus.Publish(new BlockHeadersTipChanged(height));
		if (height > filterHeaderChain.ServerTipHeight)
		{
			filterHeaderChain.SetServerTipHeight(height);
			eventBus.Publish(new NetworkTipHeightChanged(height));
		}
	}

	/// <summary>Like <see cref="ChainedBlock.GetLocator"/>, but the walk stops at the chain base instead of genesis.</summary>
	private static BlockLocator GetLocator(ChainedBlock tip)
	{
		var hashes = new List<uint256>();
		var step = 1;
		var block = tip;
		while (true)
		{
			hashes.Add(block.HashBlock);
			if (block.Previous is null)
			{
				break;
			}

			if (hashes.Count > 10)
			{
				step *= 2;
			}

			for (var i = 0; i < step && block.Previous is not null; i++)
			{
				block = block.Previous;
			}
		}

		return new BlockLocator { Blocks = hashes };
	}
}
