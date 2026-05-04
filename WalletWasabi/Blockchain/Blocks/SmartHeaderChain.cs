using NBitcoin;
using System.Collections.Generic;
using System.Threading;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// High performance chain index and cache.
/// </summary>
/// <remarks>Class is thread-safe.</remarks>
public class SmartHeaderChain
{
	private const int Unlimited = 0;

	private SmartHeader? _tip;

	private ChainHeight _serverTipHeight;

	private int _hashesLeft;

	private int _hashesCount;

	/// <param name="maxChainSize"><see cref="Unlimited"/> for no limit, otherwise the chain is capped to the specified number of elements.</param>
	public SmartHeaderChain(int maxChainSize = Unlimited)
	{
		_serverTipHeight = ChainHeight.Genesis;
		_maxChainSize = maxChainSize;
	}

	/// <remarks>Useful to save memory by removing elements at the beginning of the chain.</remarks>
	private readonly int _maxChainSize;

	private readonly LinkedList<SmartHeader> _chain = [];
	private readonly Lock _lock = new();

	public SmartHeader? Tip
	{
		get
		{
			lock (_lock)
			{
				return _tip;
			}
		}
	}

	public ChainHeight TipHeight
	{
		get
		{
			lock (_lock)
			{
				return _tip?.Height ?? ChainHeight.Genesis;
			}
		}
	}

	public uint256? TipHash
	{
		get
		{
			lock (_lock)
			{
				return _tip?.BlockHash;
			}
		}
	}

	public ChainHeight ServerTipHeight
	{
		get
		{
			lock (_lock)
			{
				return _serverTipHeight;
			}
		}
	}

	public int HashesLeft
	{
		get
		{
			lock (_lock)
			{
				return _hashesLeft;
			}
		}
	}

	/// <summary>Number of hashes in the chain.</summary>
	/// <remarks>
	/// Optimizations are taken into account for this value. So if the chain is
	/// 1000 elements long and we remove first 100 to save memory, the reported
	/// number will still be 1000.
	/// </remarks>
	public int HashCount
	{
		get
		{
			lock (_lock)
			{
				return _hashesCount;
			}
		}
	}

	/// <summary>
	/// Adds a new tip to the chain.
	/// </summary>
	public void AppendTip(SmartHeader tip)
	{
		lock (_lock)
		{
			if (_chain.Count > 0)
			{
				SmartHeader lastHeader = _chain.Last!.Value;

				if (lastHeader.Height + 1 != tip.Height)
				{
					throw new InvalidOperationException($"Header height isn't one more than the previous header height. Actual: {lastHeader.Height}. Added: {tip.Height}.");
				}
			}

			_chain.AddLast(tip);
			_hashesCount++;
			SetTipNoLock(tip);

			if (_maxChainSize != Unlimited && _chain.Count > _maxChainSize)
			{
				// Intentionally, we do not modify hashes count here.
				_chain.RemoveFirst();
			}
		}
	}

	public bool RemoveTip()
	{
		bool result = false;

		lock (_lock)
		{
			if (_chain.Count > 0)
			{
				_chain.RemoveLast();
				_hashesCount--;

				SmartHeader? newTip = _chain.Count > 0 ? _chain.Last!.Value : null;
				SetTipNoLock(newTip);

				result = true;
			}
		}

		return result;
	}

	public void SetServerTipHeight(ChainHeight height)
	{
		lock (_lock)
		{
			_serverTipHeight = height;
			SetHashesLeftNoLock();
		}
	}

	private void SetTipNoLock(SmartHeader? tip)
	{
		_tip = tip;
		SetHashesLeftNoLock();
	}

	private void SetHashesLeftNoLock()
	{
		_hashesLeft = (int)Math.Max(0, (long)_serverTipHeight - TipHeight);
	}
}
