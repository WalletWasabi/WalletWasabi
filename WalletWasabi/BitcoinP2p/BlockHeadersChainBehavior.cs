using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

/// <summary>
/// Keeps the shared block header chain in sync with the attached peer using <c>getheaders</c>/<c>headers</c>.
/// </summary>
/// <remarks>
/// This replaces NBitcoin's <see cref="ChainBehavior"/> because the chain may be rooted at a checkpoint
/// (see <see cref="BlockHeaderCheckpoints"/>) whose root block has no previous block, and
/// <see cref="ChainedBlock.GetLocator"/> walks past such a root. Headers are validated (PoW, difficulty, linkage)
/// exactly like NBitcoin does. Unlike NBitcoin, peers' <c>getheaders</c> requests are not answered.
/// </remarks>
public class BlockHeadersChainBehavior(
	ConcurrentChain blockHeaderChain,
	FilterHeaderChain filterHeaderChain,
	EventBus eventBus)
	: NodeBehavior
{
	private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

	private readonly Lock _lock = new();

	/// <summary>Tip of the headers received from this peer. Differs from the chain tip while a fork is being downloaded.</summary>
	private ChainedBlock? _pendingTip;

	private volatile bool _invalidHeaderReceived;
	private DateTime _lastSyncRequest = DateTime.MinValue;
	private int _lastPublishedHeight;

	public ConcurrentChain Chain => blockHeaderChain;

	protected override void AttachCore()
	{
		var tipHeight = blockHeaderChain.Tip?.Height ?? 0;
		if (AttachedNode.State == NodeState.Connected)
		{
			AttachedNode.MyVersion.StartHeight = tipHeight;
		}

		_lastPublishedHeight = tipHeight;
		AttachedNode.StateChanged += OnStateChanged;
		AttachedNode.MessageReceived += OnMessageReceived;
		RegisterDisposable(eventBus.Subscribe<Tick>(OnTick));
	}

	protected override void DetachCore()
	{
		AttachedNode.StateChanged -= OnStateChanged;
		AttachedNode.MessageReceived -= OnMessageReceived;
	}

	public override object Clone() =>
		new BlockHeadersChainBehavior(blockHeaderChain, filterHeaderChain, eventBus);

	private void OnStateChanged(Node node, NodeState oldState)
	{
		if (node.State == NodeState.HandShaked)
		{
			TrySync(node);
		}
	}

	private void OnTick(Tick tick)
	{
		if (AttachedNode is { } node && tick.DateTime - _lastSyncRequest >= RefreshInterval)
		{
			TrySync(node);
		}
	}

	private void OnMessageReceived(Node node, IncomingMessage message)
	{
		switch (message.Message.Payload)
		{
			case InvPayload inv when inv.Inventory.Any(i => (i.Type & InventoryType.MSG_BLOCK) != 0 && !blockHeaderChain.Contains(i.Hash)):
				TrySync(node);
				break;
			case HeadersPayload headers:
				ProcessHeaders(node, headers);
				break;
		}
	}

	private void TrySync(Node node)
	{
		if (node.State != NodeState.HandShaked || _invalidHeaderReceived)
		{
			return;
		}

		BlockLocator locator;
		lock (_lock)
		{
			if ((_pendingTip ?? blockHeaderChain.Tip) is not { } tip)
			{
				return;
			}

			locator = CreateLocator(tip);
			_lastSyncRequest = DateTime.UtcNow;
		}

		try
		{
			node.SendMessage(new GetHeadersPayload { BlockLocators = locator });
		}
		catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
		{
			Logger.LogDebug($"Could not request headers from {node.Peer.Endpoint}: {ex.Message}");
		}
	}

	private void ProcessHeaders(Node node, HeadersPayload payload)
	{
		bool tipChanged;
		int chainTipHeight;

		lock (_lock)
		{
			if ((_pendingTip ?? blockHeaderChain.Tip) is not { } tip)
			{
				return;
			}

			var (newTip, invalidHeader) = ConnectHeaders(tip, payload.Headers, blockHeaderChain, node.Network);
			if (invalidHeader is { } invalid)
			{
				_invalidHeaderReceived = true;
				Logger.LogWarning($"Peer {node.Peer.Endpoint} sent an invalid block header at height {invalid.Height} ({invalid.HashBlock}). Headers from this peer are ignored from now on.");
			}

			tipChanged = newTip != tip;
			if (tipChanged && newTip.GetChainWork(true) > blockHeaderChain.Tip!.GetChainWork(true))
			{
				blockHeaderChain.SetTip(newTip);
			}

			// Prefer the chain's instance so that a duplicate pending tip can be garbage collected.
			_pendingTip = blockHeaderChain.GetBlock(newTip.HashBlock) ?? newTip;
			chainTipHeight = blockHeaderChain.Tip!.Height;
		}

		PublishTipChanges(chainTipHeight);

		if (tipChanged && payload.Headers.Count > 0)
		{
			// The peer may have more headers for us.
			TrySync(node);
		}
	}

	private void PublishTipChanges(int currentHeight)
	{
		if (currentHeight > _lastPublishedHeight)
		{
			_lastPublishedHeight = currentHeight;
			eventBus.Publish(new BlockHeadersTipChanged((uint)currentHeight));
		}

		if (currentHeight > filterHeaderChain.ServerTipHeight)
		{
			filterHeaderChain.SetServerTipHeight((uint)currentHeight);
			eventBus.Publish(new NetworkTipHeightChanged((uint)currentHeight));
		}
	}

	/// <summary>
	/// Appends <paramref name="headers"/> to <paramref name="tip"/> as far as they connect and validate.
	/// </summary>
	/// <returns>The new tip (equal to <paramref name="tip"/> when nothing was appended) and the first invalid header, if any.</returns>
	internal static (ChainedBlock Tip, ChainedBlock? InvalidHeader) ConnectHeaders(ChainedBlock tip, IReadOnlyList<BlockHeader> headers, ConcurrentChain chain, Network network)
	{
		foreach (var header in headers)
		{
			var previous = tip.FindAncestorOrSelf(header.HashPrevBlock);
			if (previous is null)
			{
				break;
			}

			var next = new ChainedBlock(header, header.GetHash(), previous);
			var validated = chain.GetBlock(next.HashBlock) is not null || next.Validate(network);
			if (!validated)
			{
				return (tip, next);
			}

			tip = next;
		}

		return (tip, null);
	}

	/// <summary>
	/// Builds a block locator like <see cref="ChainedBlock.GetLocator"/> (exponentially spaced heights) but stops at
	/// the root of a chain that does not start at genesis.
	/// </summary>
	internal static BlockLocator CreateLocator(ChainedBlock tip)
	{
		var hashes = new List<uint256>();
		var step = 1;
		var block = tip;

		while (block is not null)
		{
			hashes.Add(block.HashBlock);
			if (block.Height == 0)
			{
				break;
			}

			var targetHeight = Math.Max(block.Height - step, 0);
			while (block is not null && block.Height > targetHeight)
			{
				block = block.Previous;
			}

			if (hashes.Count > 10)
			{
				step *= 2;
			}
		}

		return new BlockLocator { Blocks = hashes };
	}
}
