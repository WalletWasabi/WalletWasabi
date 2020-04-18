using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Wallets
{
	/// <summary>
	/// SmartP2pBlockProvider is a blocks provider that can provides 
	/// blocks from multiple requesters.
	/// </summary>
	public class SmartBlockProvider : IBlockProvider
	{
		public SmartBlockProvider(IBlockProvider provider)
		{
			InnerBlockProvider = provider;
		}

		private AsyncLock Lock { get; } = new AsyncLock();
		
		private Dictionary<uint256, Task<Block>> Requests = new Dictionary<uint256, Task<Block>>();
		
		private IBlockProvider InnerBlockProvider { get; }

		public Task<Block> GetBlockAsync(uint256 hash, CancellationToken cancel)
		{
			lock (Lock)
			{
				if (!Requests.TryGetValue(hash, out var getBlockTask))
				{
					getBlockTask = InnerBlockProvider.GetBlockAsync(hash, cancel);
					Requests.Add(hash, getBlockTask);
				}
				return getBlockTask;
			}
		}
	}
}