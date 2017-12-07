using System;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HiddenWallet.KeyManagement;
using HiddenWallet.Models;

namespace HiddenWallet.Tests
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

		public static Dictionary<uint256, List<BalanceOperation>> GetOperationsPerTransactions(Dictionary<Script, List<BalanceOperation>> operationsPerScriptPubKeys)
		{
			// 1. Get all the unique operations
			var opSet = new HashSet<BalanceOperation>();
			foreach (var elem in operationsPerScriptPubKeys)
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

		public static async Task<Dictionary<Script, List<BalanceOperation>>> QueryOperationsPerSafeScriptPubKeysAsync(QBitNinjaClient client, Safe safe, int minUnusedKeys = 7, HdPathType? hdPathType = null)
		{
			if (hdPathType == null)
			{
				var t1 = QueryOperationsPerSafeScriptPubKeysAsync(client, safe, minUnusedKeys, HdPathType.Receive);
				var t2 = QueryOperationsPerSafeScriptPubKeysAsync(client, safe, minUnusedKeys, HdPathType.Change);
				var t3 = QueryOperationsPerSafeScriptPubKeysAsync(client, safe, minUnusedKeys, HdPathType.NonHardened);

				await Task.WhenAll(t1, t2, t3);

				Dictionary<Script, List<BalanceOperation>> operationsPerReceiveScriptPubKeys = await t1;
				Dictionary<Script, List<BalanceOperation>> operationsPerChangeScriptPubKeys = await t2;
				Dictionary<Script, List<BalanceOperation>> operationsPerNonHardenedScriptPubKeys = await t3;

				var operationsPerAllScriptPubKeys = new Dictionary<Script, List<BalanceOperation>>();
				foreach (var elem in operationsPerReceiveScriptPubKeys)
					operationsPerAllScriptPubKeys.Add(elem.Key, elem.Value);
				foreach (var elem in operationsPerChangeScriptPubKeys)
					operationsPerAllScriptPubKeys.Add(elem.Key, elem.Value);
				foreach (var elem in operationsPerNonHardenedScriptPubKeys)
					operationsPerAllScriptPubKeys.Add(elem.Key, elem.Value);

				return operationsPerAllScriptPubKeys;
			}

            var scriptPubKeys = new List<Script>();
            var addressTypes = new HashSet<AddressType>
            {
                AddressType.Pay2PublicKeyHash,
                AddressType.Pay2WitnessPublicKeyHash
            };
            foreach (AddressType addressType in addressTypes)
            {
                foreach (var scriptPubKey in safe.GetFirstNScriptPubKey(addressType, minUnusedKeys, hdPathType.GetValueOrDefault()))
                {
					scriptPubKeys.Add(scriptPubKey);
                }
            }

			var operationsPerScriptPubKeys = new Dictionary<Script, List<BalanceOperation>>();
			var unusedKeyCount = 0;
			foreach (var elem in await QueryOperationsPerScriptPubKeysAsync(client, scriptPubKeys))
			{
				operationsPerScriptPubKeys.Add(elem.Key, elem.Value);
				if (elem.Value.Count == 0) unusedKeyCount++;
			}

			Debug.WriteLine($"{operationsPerScriptPubKeys.Count} {hdPathType} keys are processed.");

			var startIndex = minUnusedKeys;
			while (unusedKeyCount < minUnusedKeys)
			{
				scriptPubKeys = new List<Script>();
				for (int i = startIndex; i < startIndex + minUnusedKeys; i++)
				{
                    addressTypes = new HashSet<AddressType>
                    {
                        AddressType.Pay2PublicKeyHash,
                        AddressType.Pay2WitnessPublicKeyHash
                    };
                    foreach (AddressType addressType in addressTypes)
                    {
						scriptPubKeys.Add(safe.GetScriptPubKey(addressType, i, hdPathType.GetValueOrDefault()));
                    }
                }
				foreach (var elem in await QueryOperationsPerScriptPubKeysAsync(client, scriptPubKeys))
				{
					operationsPerScriptPubKeys.Add(elem.Key, elem.Value);
					if (elem.Value.Count == 0) unusedKeyCount++;
				}

				Debug.WriteLine($"{operationsPerScriptPubKeys.Count} {hdPathType} keys are processed.");
				startIndex += minUnusedKeys;
			}

			return operationsPerScriptPubKeys;
		}

		public static async Task<Dictionary<Script, List<BalanceOperation>>> QueryOperationsPerScriptPubKeysAsync(QBitNinjaClient client, IEnumerable<Script> scriptPubKeys)
		{
			var operationsPerScriptPubKeys = new Dictionary<Script, List<BalanceOperation>>();

			var scriptPubKeyList = scriptPubKeys.ToList();
			var balanceModelList = new List<BalanceModel>();

			foreach (var balance in await GetBalancesAsync(client, scriptPubKeyList, unspentOnly: false))
			{
				balanceModelList.Add(balance);
			}

			for (var i = 0; i < balanceModelList.Count; i++)
			{
				operationsPerScriptPubKeys.Add(scriptPubKeyList[i], balanceModelList[i].Operations);
			}

			return operationsPerScriptPubKeys;
		}

		private static async Task<IEnumerable<BalanceModel>> GetBalancesAsync(QBitNinjaClient client, IEnumerable<Script> scriptPubKeys, bool unspentOnly)
		{
			var tasks = new HashSet<Task<BalanceModel>>();
			foreach (var dest in scriptPubKeys)
			{
				var task = client.GetBalance(dest, unspentOnly);

				tasks.Add(task);
			}

			await Task.WhenAll(tasks);

			var results = new HashSet<BalanceModel>();
			foreach (var task in tasks)
				results.Add(await task);

			return results;
		}
	}
}
