namespace WalletWasabi.BitcoinP2p;

/// <summary>
/// Knows whether the block header chain has caught up with the peers.
/// It is shared by the <see cref="BlockHeaderSyncBehavior"/> of every node.
/// </summary>
/// <remarks>
/// Block headers are the first component to download. The filter headers, the compact filters and the blocks
/// wait until the block headers are synchronized.
/// </remarks>
public class BlockHeaderSynchronizationState
{
	private readonly Lock _lock = new();
	private bool _isSynchronized;

	/// <summary>
	/// <c>true</c> once a node that is not behind the chain had no more headers to send,
	/// <c>false</c> while there are headers left to download.
	/// </summary>
	public bool IsSynchronized
	{
		get
		{
			lock (_lock)
			{
				return _isSynchronized;
			}
		}
	}

	/// <summary>
	/// Updates the state after the chain processed a <c>headers</c> message of a node.
	/// </summary>
	/// <param name="isFullMessage">
	/// <c>true</c> if the message had as many headers as fit in one. A node sends fewer only when it has no more.
	/// </param>
	/// <param name="hasExtendedChain"><c>true</c> if the message gave the chain a new tip.</param>
	/// <param name="chainTip">Tip of the chain after the message.</param>
	/// <param name="nodeTip">The last header that the node is known to have: it has nothing to send after it.</param>
	internal void OnHeadersProcessed(bool isFullMessage, bool hasExtendedChain, ChainedBlock chainTip, ChainedBlock nodeTip)
	{
		// A full message means that the node has more headers. They are missing from the chain if these ones were.
		// Otherwise the node has no more headers, which tells that the chain is at the tip unless the node is behind it.
		bool? isSynchronized = isFullMessage
			? (hasExtendedChain ? false : null)
			: (nodeTip.Height >= chainTip.Height ? true : null);

		lock (_lock)
		{
			if (isSynchronized is null || _isSynchronized == isSynchronized)
			{
				return;
			}

			_isSynchronized = isSynchronized.Value;
		}

		Logger.LogInfo(isSynchronized.Value
			? $"Block headers are synchronized up to height {chainTip.Height}."
			: $"Block headers are behind the peers, downloading them from height {chainTip.Height}.");
	}
}
