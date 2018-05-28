using ConcurrentCollections;
using WalletWasabi.Models;
using NBitcoin;
using System;

namespace WalletWasabi.Services
{
	public class MemPoolService
	{
		public ConcurrentHashSet<uint256> TransactionHashes { get; }

		public event EventHandler<SmartTransaction> TransactionReceived;

		internal void OnTransactionReceived(SmartTransaction transaction) => TransactionReceived?.Invoke(this, transaction);

		public MemPoolService()
		{
			TransactionHashes = new ConcurrentHashSet<uint256>();
		}
	}
}
