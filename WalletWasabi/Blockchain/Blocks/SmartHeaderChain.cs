using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Blocks
{
	/// <summary>
	/// High performance chain index and cache.
	/// </summary>
	public class SmartHeaderChain : NotifyPropertyChangedBase
	{
		private uint _tipHeight;
		private SmartHeader _tip;
		private uint256 _tipHash;
		private uint _serverTipHeight;
		private int _hashesLeft;
		private int _hashesCount;

		private SortedDictionary<uint, SmartHeader> Chain { get; }
		private object Lock { get; }

		public SmartHeader Tip
		{
			get => _tip;
			private set => RaiseAndSetIfChanged(ref _tip, value);
		}

		public uint TipHeight
		{
			get => _tipHeight;
			private set => RaiseAndSetIfChanged(ref _tipHeight, value);
		}

		public uint256 TipHash
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

		public SmartHeaderChain()
		{
			Chain = new SortedDictionary<uint, SmartHeader>();
			Lock = new object();
		}

		public void AddOrReplace(SmartHeader header)
		{
			lock (Lock)
			{
				var lastHeader = Chain.LastOrDefault().Value;
				if (lastHeader is { BlockHash: var prevHash } && prevHash != header.PrevHash)
				{
					throw new InvalidOperationException($"Header doesn't point to previous header. Actual: {prevHash}, tried: {header.PrevHash}.");
				}

				Chain.AddOrReplace(header.Height, header);
				SetTip(header);
			}
		}

		private void SetTip(SmartHeader header)
		{
			Tip = header;
			TipHeight = header.Height;
			TipHash = header.BlockHash;
			HashCount = Chain.Count;
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
					Chain.Remove(Chain.Keys.Max());
					var last = Chain.Last();
					SetTip(last.Value);
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

		public bool TryGetHeight(uint256 hash, out uint height)
		{
			lock (Lock)
			{
				height = Chain.FirstOrDefault(x => x.Value.BlockHash == hash).Key;

				// Default int will be 0. We do not know if this refers to the 0th hash or it just means the hash was not found.
				// So let's check if the height contains or not.
				// If the given height is 0, then check if the chain has a key with 0. If it does not have, then return false. If it has, check if the hash is the same or not.
				if (height == 0 && (!Chain.ContainsKey(0) || Chain[0].BlockHash != hash))
				{
					return false;
				}
				return true;
			}
		}
	}
}
