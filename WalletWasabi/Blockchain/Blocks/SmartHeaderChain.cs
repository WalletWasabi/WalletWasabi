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

	private Dictionary<uint, SmartHeader> Chain { get; } = new();
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

	public void AddOrReplace(SmartHeader header)
	{
		lock (Lock)
		{
			if (Chain.TryGetValue(TipHeight, out var lastHeader))
			{
				if (lastHeader.BlockHash != header.PrevHash)
				{
					throw new InvalidOperationException($"Header doesn't point to previous header. Actual: {lastHeader.PrevHash}. Expected: {header.PrevHash}.");
				}

				if (lastHeader.Height != header.Height - 1)
				{
					throw new InvalidOperationException($"Header height isn't one more than the previous header height. Actual: {lastHeader.Height}. Expected: {header.Height - 1}.");
				}
			}

			Chain.Add(header.Height, header);
			SetTipNoLock(header);
		}
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private void SetTipNoLock(SmartHeader? header)
	{
		_tip = header;
		_tipHeight = header?.Height ?? default;
		_tipHash = header?.BlockHash;
		_hashesCount = Chain?.Count ?? default;
		SetHashesLeftNoLock();
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private void SetHashesLeftNoLock()
	{
		_hashesLeft = (int)Math.Max(0, (long)_serverTipHeight - _tipHeight);
	}

	public bool RemoveTip()
	{
		bool result = false;

		lock (Lock)
		{
			if (Chain.Any())
			{
				result = Chain.Remove(Chain.Last().Key);
				if (Chain.Any())
				{
					var newLast = Chain.Last();
					SetTipNoLock(newLast.Value);
				}
				else
				{
					SetTipNoLock(null);
				}
			}
		}

		return result;
	}

	public void UpdateServerTipHeight(uint height)
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
			return Chain.Select(x => (x.Key, x.Value)).ToArray();
		}
	}
}
