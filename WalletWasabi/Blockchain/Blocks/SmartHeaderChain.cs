using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// High performance chain index and cache.
/// </summary>
/// <remarks>Class is thread-safe.</remarks>
public class SmartHeaderChain
{
	public const int Unlimited = 0;

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

	/// <param name="maxChainSize"><see cref="Unlimited"/> for no limit, otherwise the chain is capped to the specified number of elements.</param>
	public SmartHeaderChain(int maxChainSize = Unlimited)
	{
		MaxChainSize = maxChainSize;
	}

	/// <summary>Task completion source that is completed once a <see cref="ServerTipHeight"/> is initialized for the first time.</summary>
	public TaskCompletionSource ServerTipInitializedTcs { get; } = new();

	/// <remarks>Useful to save memory by removing elements at the beginning of the chain.</remarks>
	private int MaxChainSize { get; }

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
			_hashesCount++;
			SetTipNoLock(tip);

			if (MaxChainSize != Unlimited && Chain.Count > MaxChainSize)
			{
				// Intentionally, we do not modify hashes count here.
				Chain.RemoveFirst();
			}
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
				_hashesCount--;

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

		ServerTipInitializedTcs.TrySetResult();
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
		SetHashesLeftNoLock();
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private void SetHashesLeftNoLock()
	{
		_hashesLeft = (int)Math.Max(0, (long)_serverTipHeight - _tipHeight);
	}
}
