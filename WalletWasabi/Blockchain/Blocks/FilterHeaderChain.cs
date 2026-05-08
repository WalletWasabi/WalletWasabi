using NBitcoin;
using System.Collections.Generic;
using System.Threading;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// High performance chain index and cache.
/// </summary>
/// <remarks>Class is thread-safe.</remarks>
public class FilterHeaderChain
{
	private SmartHeader? _tip;

	private ChainHeight _serverTipHeight = ChainHeight.Genesis;

	private int _hashesLeft;

	private int _hashesCount;

	private readonly List<SmartHeader> _chain = [];
	private readonly Lock _lock = new();
	private uint _baseHeight;

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
				SmartHeader lastHeader = _chain[^1];

				if (lastHeader.Height + 1 != tip.Height)
				{
					throw new InvalidOperationException($"Header height isn't one more than the previous header height. Actual: {lastHeader.Height}. Added: {tip.Height}.");
				}
			}
			else
			{
				// First element - set the base height
				_baseHeight = tip.Height;
			}

			_chain.Add(tip);
			_hashesCount++;
			SetTipNoLock(tip);
		}
	}

	public bool RemoveTip()
	{
		bool result = false;

		lock (_lock)
		{
			if (_chain.Count > 0)
			{
				_chain.RemoveAt(_chain.Count - 1);
				_hashesCount--;

				SmartHeader? newTip = _chain.Count > 0 ? _chain[^1] : null;
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

	public SmartHeader? this[uint height]
	{
		get
		{
			lock (_lock)
			{
				var index = (int)(height - _baseHeight);
				if (index < 0 || index >= _chain.Count)
				{
					return null;
				}
				return _chain[index];
			}
		}
	}
}
