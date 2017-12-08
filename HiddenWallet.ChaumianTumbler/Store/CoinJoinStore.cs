using ConcurrentCollections;
using NBitcoin;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Store
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CoinJoinStore
    {
		[JsonProperty(PropertyName = "Transactions")]
		public ConcurrentHashSet<CoinJoinTransaction> Transactions { get; private set; }

		public CoinJoinStore()
		{
			Transactions = new ConcurrentHashSet<CoinJoinTransaction>();
		}

		private static readonly AsyncLock _asyncLock = new AsyncLock();

		public bool TryUpdateState(uint256 txid, CoinJoinTransactionState newState)
		{
			using (_asyncLock.Lock())
			{
				CoinJoinTransaction tx = Transactions.SingleOrDefault(x => x.Transaction.GetHash() == txid);
				if(tx == default(CoinJoinTransaction))
				{
					return false;
				}
				if(tx.State == newState)
				{
					return false;
				}
				tx.State = newState;
				return true;
			}
		}

		public async Task ToFileAsync(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));

			using (await _asyncLock.LockAsync())
			{
				string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
				await File.WriteAllTextAsync(
					filePath,
					jsonString,
					Encoding.UTF8);
			}
		}

		public async static Task<CoinJoinStore> CreateFromFileAsync(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));

			using (await _asyncLock.LockAsync())
			{
				string jsonString = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
				return JsonConvert.DeserializeObject<CoinJoinStore>(jsonString);
			}
		}
	}
}
