using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// High performance chain index and cache.
	/// </summary>
	public class HashChain : INotifyPropertyChanged
	{
		private int _tipHeight;
		private uint256 _tipHash;
		private int _serverTipHeight;
		private int _hashesLeft;
		private int _hashesCount;

		private SortedDictionary<int, uint256> Chain { get; }
		private object Lock { get; }

		public int TipHeight
		{
			get => _tipHeight;
			private set
			{
				if (_tipHeight != value)
				{
					_tipHeight = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TipHeight)));
				}
			}
		}

		public uint256 TipHash
		{
			get => _tipHash;
			private set
			{
				if (_tipHash != value)
				{
					_tipHash = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TipHash)));
				}
			}
		}

		public int ServerTipHeight
		{
			get => _serverTipHeight;
			private set
			{
				if (_serverTipHeight != value)
				{
					_serverTipHeight = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ServerTipHeight)));
				}
			}
		}

		public int HashesLeft
		{
			get => _hashesLeft;
			private set
			{
				if (_hashesLeft != value)
				{
					_hashesLeft = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HashesLeft)));
				}
			}
		}

		public int HashCount
		{
			get => _hashesCount;
			private set
			{
				if (_hashesCount != value)
				{
					_hashesCount = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HashCount)));
				}
			}
		}

		public HashChain()
		{
			Chain = new SortedDictionary<int, uint256>();
			Lock = new object();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void AddOrReplace(int height, uint256 hash)
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
			HashesLeft = Math.Max(0, ServerTipHeight - TipHeight);
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

		public void UpdateServerTipHeight(int height)
		{
			lock (Lock)
			{
				ServerTipHeight = height;
				SetHashesLeft();
			}
		}

		public (int height, uint256 hash)[] GetChain()
		{
			lock (Lock)
			{
				return Chain.Select(x => (x.Key, x.Value)).ToArray();
			}
		}
	}
}
