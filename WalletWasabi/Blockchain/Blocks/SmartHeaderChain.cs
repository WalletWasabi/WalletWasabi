using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Bases;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// High performance chain index and cache.
/// </summary>
public class SmartHeaderChain : NotifyPropertyChangedBase
{
	private uint _tipHeight;
	private SmartHeader? _tip;
	private uint256? _tipHash;
	private uint _serverTipHeight;
	private int _hashesLeft;
	private int _hashesCount;

	private Dictionary<uint, SmartHeader> Chain { get; } = new Dictionary<uint, SmartHeader>();
	private object Lock { get; } = new object();

	public SmartHeader? Tip
	{
		get => _tip;
		private set => RaiseAndSetIfChanged(ref _tip, value);
	}

	public uint TipHeight
	{
		get => _tipHeight;
		private set => RaiseAndSetIfChanged(ref _tipHeight, value);
	}

	public uint256? TipHash
	{
		get => _tipHash;
		private set => RaiseAndSetIfChanged(ref _tipHash, value);
	}

	public uint ServerTipHeight
	{
		get => _serverTipHeight;
		private set => RaiseAndSetIfChanged(ref _serverTipHeight, value);
	}

	public int HashesLeft
	{
		get => _hashesLeft;
		private set => RaiseAndSetIfChanged(ref _hashesLeft, value);
	}

	public int HashCount
	{
		get => _hashesCount;
		private set => RaiseAndSetIfChanged(ref _hashesCount, value);
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
			SetTip(header);
		}
	}

	private void SetTip(SmartHeader? header)
	{
		Tip = header;
		TipHeight = header?.Height ?? default;
		TipHash = header?.BlockHash;
		HashCount = Chain?.Count ?? default;
		SetHashesLeft();
	}

	private void SetHashesLeft()
	{
		HashesLeft = (int)Math.Max(0, (long)ServerTipHeight - TipHeight);
	}

	public void RemoveLast()
	{
		lock (Lock)
		{
			if (Chain.Any())
			{
				Chain.Remove(Chain.Last().Key);
				if (Chain.Any())
				{
					var newLast = Chain.Last();
					SetTip(newLast.Value);
				}
				else
				{
					SetTip(null);
				}
			}
		}
	}

	public void UpdateServerTipHeight(uint height)
	{
		lock (Lock)
		{
			ServerTipHeight = height;
			SetHashesLeft();
		}
	}

	public (uint height, SmartHeader header)[] GetChain()
	{
		lock (Lock)
		{
			return Chain.Select(x => (x.Key, x.Value)).ToArray();
		}
	}
}
