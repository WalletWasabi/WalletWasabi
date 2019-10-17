using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// High performance chain index and cache.
	/// </summary>
	public class HashChain : NotifyPropertyChangedBase
	{
		private uint _tipHeight;
		private uint256 _tipHash;
		private uint _serverTipHeight;
		private int _hashesLeft;
		private int _hashesCount;

		private SortedDictionary<uint, uint256> Chain { get; }
		private object Lock { get; }

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

		public HashChain()
		{
			Chain = new SortedDictionary<uint, uint256>();
			Lock = new object();
		}

		public void AddOrReplace(uint height, uint256 hash)
		{
			lock (Lock)
			{
				Chain.AddOrReplace(height, hash);
				TipHeight = height;
				TipHash = hash;
				HashCount = Chain.Count;
				SetHashesLeft();
			}
		}

		private void SetHashesLeft()
		{
			var hashesLeft = (int)ServerTipHeight - (int)TipHeight;
			HashesLeft = Math.Max(0, hashesLeft);
		}

		public void RemoveLast()
		{
			lock (Lock)
			{
				if (Chain.Any())
				{
					Chain.Remove(Chain.Keys.Max());
					var last = Chain.Last();
					TipHeight = last.Key;
					TipHash = last.Value;
					HashCount = Chain.Count;
					SetHashesLeft();
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

		public (uint height, uint256 hash)[] GetChain()
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
				height = Chain.FirstOrDefault(x => x.Value == hash).Key;

				// Default int will be 0. We do not know if this refers to the 0th hash or it just means the hash was not found.
				// So let's check if the height contains or not.
				// If the given height is 0, then check if the chain has a key with 0. If it does not have, then return false. If it has, check if the hash is the same or not.
				if (height == 0 && (!Chain.ContainsKey(0) || Chain[0] != hash))
				{
					return false;
				}
				return true;
			}
		}
	}
}
