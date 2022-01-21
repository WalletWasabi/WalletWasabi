using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// High performance chain index and cache.
/// </summary>
public class SmartHeaderChain
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private uint _tipHeight;

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private SmartHeader? _tip;

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private uint256? _tipHash;

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private uint _serverTipHeight;

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private int _hashesLeft;

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private int _hashesCount;

	private LinkedList<SmartHeader> Chain { get; } = new();
	private object Lock { get; } = new();

	public SmartHeader? Tip
	{
		get
		{
			lock (Lock)
			{
				return _tip;
			}
		}
	}

	public uint TipHeight
	{
		get
		{
			lock (Lock)
			{
				return _tipHeight;
			}
		}
	}

	public uint256? TipHash
	{
		get
		{
			lock (Lock)
			{
				return _tipHash;
			}
		}
	}

	public uint ServerTipHeight
	{
		get
		{
			lock (Lock)
			{
				return _serverTipHeight;
			}
		}
	}

	public int HashesLeft
	{
		get
		{
			lock (Lock)
			{
				return _hashesLeft;
			}
		}
	}

	public int HashCount
	{
		get
		{
			lock (Lock)
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
		lock (Lock)
		{
			if (Chain.Count > 0)
			{
				SmartHeader lastHeader = Chain.Last!.Value;

				if (lastHeader.BlockHash != tip.PrevHash)
				{
					throw new InvalidOperationException($"Header doesn't point to previous header. Actual: {lastHeader.PrevHash}. Expected: {tip.PrevHash}.");
				}

				if (lastHeader.Height != tip.Height - 1)
				{
					throw new InvalidOperationException($"Header height isn't one more than the previous header height. Actual: {lastHeader.Height}. Expected: {tip.Height - 1}.");
				}
			}

			Chain.AddLast(tip);
			SetTipNoLock(tip);
		}
	}

	public bool RemoveTip()
	{
		bool result = false;

		lock (Lock)
		{
			if (Chain.Count > 0)
			{
				Chain.RemoveLast();

				SmartHeader? newTip = (Chain.Count > 0) ? Chain.Last!.Value : null;
				SetTipNoLock(newTip);

				result = true;
			}
		}

		return result;
	}

	public void SetServerTipHeight(uint height)
	{
		lock (Lock)
		{
			_serverTipHeight = height;
			SetHashesLeftNoLock();
		}
	}

	/// <remarks>Only for tests.</remarks>
	public (uint height, SmartHeader header)[] GetChain()
	{
		lock (Lock)
		{
			return Chain.Select(x => (x.Height, x)).ToArray();
		}
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private void SetTipNoLock(SmartHeader? tip)
	{
		_tip = tip;
		_tipHeight = tip?.Height ?? default;
		_tipHash = tip?.BlockHash;
		_hashesCount = Chain?.Count ?? default;
		SetHashesLeftNoLock();
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private void SetHashesLeftNoLock()
	{
		_hashesLeft = (int)Math.Max(0, (long)_serverTipHeight - _tipHeight);
	}
}
