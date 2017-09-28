using System;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HBitcoin.KeyManagement;

namespace HBitcoin.Tests
{
	public static class QBitNinjaJutsus
	{
		public static void GetBalances(IEnumerable<AddressHistoryRecord> addressHistoryRecords, out Money confirmedBalance, out Money unconfirmedBalance)
		{
			confirmedBalance = Money.Zero;
			unconfirmedBalance = Money.Zero;
			foreach (var record in addressHistoryRecords)
			{
				if (record.Confirmed)
					confirmedBalance += record.Amount;
				else
				{
					unconfirmedBalance += record.Amount;
				}
			}
		}

		public static bool SelectCoins(ref HashSet<Coin> coinsToSpend, Money totalOutAmount, IEnumerable<Coin> unspentCoins)
		{
			var haveEnough = false;
			foreach (Coin coin in unspentCoins.OrderByDescending(x => x.Amount))
			{
				coinsToSpend.Add(coin);
				// if doesn't reach amount, continue adding next coin
				if (coinsToSpend.Sum(x => x.Amount) < totalOutAmount) continue;

				haveEnough = true;
				break;
			}

			return haveEnough;
		}

		public static Dictionary<uint256, List<BalanceOperation>> GetOperationsPerTransactions(Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses)
		{
			// 1. Get all the unique operations
			var opSet = new HashSet<BalanceOperation>();
			foreach (var elem in operationsPerAddresses)
				foreach (var op in elem.Value)
					opSet.Add(op);

			if (!opSet.Any()) throw new NotSupportedException();

			// 2. Get all operations, grouped by transactions
			var operationsPerTransactions = new Dictionary<uint256, List<BalanceOperation>>();
			foreach (var op in opSet)
			{
				var txId = op.TransactionId;
                if (operationsPerTransactions.TryGetValue(txId, out List<BalanceOperation> ol))
                {
                    ol.Add(op);
                    operationsPerTransactions[txId] = ol;
                }
                else operationsPerTransactions.Add(txId, new List<BalanceOperation> { op });
            }

			return operationsPerTransactions;
		}

		public static async Task<Dictionary<BitcoinAddress, List<BalanceOperation>>> QueryOperationsPerSafeAddressesAsync(QBitNinjaClient client, Safe safe, int minUnusedKeys = 7, HdPathType? hdPathType = null)
		{
			if (hdPathType == null)
			{
				var t1 = QueryOperationsPerSafeAddressesAsync(client, safe, minUnusedKeys, HdPathType.Receive);
				var t2 = QueryOperationsPerSafeAddressesAsync(client, safe, minUnusedKeys, HdPathType.Change);
				var t3 = QueryOperationsPerSafeAddressesAsync(client, safe, minUnusedKeys, HdPathType.NonHardened);

				await Task.WhenAll(t1, t2, t3).ConfigureAwait(false);

				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerReceiveAddresses = await t1.ConfigureAwait(false);
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerChangeAddresses = await t2.ConfigureAwait(false);
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerNonHardenedAddresses = await t3.ConfigureAwait(false);

				var operationsPerAllAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>();
				foreach (var elem in operationsPerReceiveAddresses)
					operationsPerAllAddresses.Add(elem.Key, elem.Value);
				foreach (var elem in operationsPerChangeAddresses)
					operationsPerAllAddresses.Add(elem.Key, elem.Value);
				foreach (var elem in operationsPerNonHardenedAddresses)
					operationsPerAllAddresses.Add(elem.Key, elem.Value);

				return operationsPerAllAddresses;
			}

			var addresses = safe.GetFirstNAddresses(minUnusedKeys, hdPathType.GetValueOrDefault());
			//var addresses = FakeData.FakeSafe.GetFirstNAddresses(minUnusedKeys);

			var operationsPerAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>();
			var unusedKeyCount = 0;
			foreach (var elem in await QueryOperationsPerAddressesAsync(client, addresses).ConfigureAwait(false))
			{
				operationsPerAddresses.Add(elem.Key, elem.Value);
				if (elem.Value.Count == 0) unusedKeyCount++;
			}

			Debug.WriteLine($"{operationsPerAddresses.Count} {hdPathType} keys are processed.");

			var startIndex = minUnusedKeys;
			while (unusedKeyCount < minUnusedKeys)
			{
				addresses = new List<BitcoinAddress>();
				for (int i = startIndex; i < startIndex + minUnusedKeys; i++)
				{
					addresses.Add(safe.GetAddress(i, hdPathType.GetValueOrDefault()));
					//addresses.Add(FakeData.FakeSafe.GetAddress(i));
				}
				foreach (var elem in await QueryOperationsPerAddressesAsync(client, addresses).ConfigureAwait(false))
				{
					operationsPerAddresses.Add(elem.Key, elem.Value);
					if (elem.Value.Count == 0) unusedKeyCount++;
				}

				Debug.WriteLine($"{operationsPerAddresses.Count} {hdPathType} keys are processed.");
				startIndex += minUnusedKeys;
			}

			return operationsPerAddresses;
		}

		public static async Task<Dictionary<BitcoinAddress, List<BalanceOperation>>> QueryOperationsPerAddressesAsync(QBitNinjaClient client, IEnumerable<BitcoinAddress> addresses)
		{
			var operationsPerAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>();

			var addressList = addresses.ToList();
			var balanceModelList = new List<BalanceModel>();

			foreach (var balance in await GetBalancesAsync(client, addressList, unspentOnly: false).ConfigureAwait(false))
			{
				balanceModelList.Add(balance);
			}

			for (var i = 0; i < balanceModelList.Count; i++)
			{
				operationsPerAddresses.Add(addressList[i], balanceModelList[i].Operations);
			}

			return operationsPerAddresses;
		}

		private static async Task<IEnumerable<BalanceModel>> GetBalancesAsync(QBitNinjaClient client, IEnumerable<BitcoinAddress> addresses, bool unspentOnly)
		{
			var tasks = new HashSet<Task<BalanceModel>>();
			foreach (var dest in addresses)
			{
				var task = client.GetBalance(dest, unspentOnly);

				tasks.Add(task);
			}

			await Task.WhenAll(tasks).ConfigureAwait(false);

			var results = new HashSet<BalanceModel>();
			foreach (var task in tasks)
				results.Add(await task.ConfigureAwait(false));

			return results;
		}
	}
}
