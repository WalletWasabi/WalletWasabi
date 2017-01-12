using DotNetTor;
using HiddenWallet.KeyManagement;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static HiddenWallet.KeyManagement.Safe;
using static System.Console;

namespace HiddenWallet.QBitNinjaJutsus
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

		public static bool SelectCoins(ref HashSet<Coin> coinsToSpend, Money totalOutAmount, List<Coin> unspentCoins)
		{
			var haveEnough = false;
			foreach (var coin in unspentCoins.OrderByDescending(x => x.Amount))
			{
				coinsToSpend.Add(coin);
				// if doesn't reach amount, continue adding next coin
				if (coinsToSpend.Sum(x => x.Amount) < totalOutAmount) continue;
				else
				{
					haveEnough = true;
					break;
				}
			}

			return haveEnough;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="secrets"></param>
		/// <returns>dictionary with coins and if confirmed</returns>
		public static async Task<Dictionary<Coin, bool>> GetUnspentCoinsAsync(IEnumerable<ISecret> secrets)
		{
			try
			{
				var client = new QBitNinjaClient(Config.Network);
				Dictionary<Coin, bool> unspentCoins;
				if (Config.UseTor)
				{
					using (var socksPortClient = new DotNetTor.SocksPort.Client(Config.TorHost, Config.TorSocksPort))
					{
						var handler = socksPortClient.GetHandlerFromRequestUri(client.BaseAddress.AbsoluteUri);
						client.SetHttpMessageHandler(handler);
						unspentCoins = await GetUnspentCoinsAsync(secrets, client).ConfigureAwait(false);
					}
				}
				else
				{
					unspentCoins = await GetUnspentCoinsAsync(secrets, client).ConfigureAwait(false);
				}

				return unspentCoins;
			}
			catch (TorException ex)
			{
				string message = ex.Message + Environment.NewLine +
					"You are not running TOR or your TOR settings are misconfigured." + Environment.NewLine +
					$"Please review your 'torrc' and '{ConfigFileSerializer.ConfigFilePath}' files.";
				throw new Exception(message, ex);
			}
		}

		private static async Task<Dictionary<Coin, bool>> GetUnspentCoinsAsync(IEnumerable<ISecret> secrets, QBitNinjaClient client)
		{
			var unspentCoins = new Dictionary<Coin, bool>();

			var addresses = new HashSet<BitcoinAddress>();
			foreach (var secret in secrets)
			{
				addresses.Add(secret.PrivateKey.ScriptPubKey.GetDestinationAddress(Config.Network));
			}

			foreach (var balanceModel in await GetBalancesAsync(client, addresses, unspentOnly: true).ConfigureAwait(false))
			{
				foreach (var operation in balanceModel.Operations)
				{
					foreach (var elem in operation.ReceivedCoins.Select(coin => coin as Coin))
					{
						unspentCoins.Add(elem, operation.Confirmations > 0);
					}
				}
			}

			return unspentCoins;
		}

		public static Dictionary<uint256, List<BalanceOperation>> GetOperationsPerTransactions(Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerAddresses)
		{
			// 1. Get all the unique operations
			var opSet = new HashSet<BalanceOperation>();
			foreach (var elem in operationsPerAddresses)
				foreach (var op in elem.Value)
					opSet.Add(op);
			if (opSet.Count() == 0) Program.Exit("Wallet has no history yet.");

			// 2. Get all operations, grouped by transactions
			var operationsPerTransactions = new Dictionary<uint256, List<BalanceOperation>>();
			foreach (var op in opSet)
			{
				var txId = op.TransactionId;
				List<BalanceOperation> ol;
				if (operationsPerTransactions.TryGetValue(txId, out ol))
				{
					ol.Add(op);
					operationsPerTransactions[txId] = ol;
				}
				else operationsPerTransactions.Add(txId, new List<BalanceOperation> { op });
			}

			return operationsPerTransactions;
		}

		public static async Task<Dictionary<BitcoinAddress, List<BalanceOperation>>> QueryOperationsPerSafeAddressesAsync(Safe safe, int minUnusedKeys = 7, HdPathType? hdPathType = null)
		{
			if (hdPathType == null)
			{
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerReceiveAddresses = await QueryOperationsPerSafeAddressesAsync(safe, minUnusedKeys, HdPathType.Receive).ConfigureAwait(false);
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerChangeAddresses = await QueryOperationsPerSafeAddressesAsync(safe, minUnusedKeys, HdPathType.Change).ConfigureAwait(false);
				Dictionary<BitcoinAddress, List<BalanceOperation>> operationsPerNonHardenedAddresses = await QueryOperationsPerSafeAddressesAsync(safe, minUnusedKeys, HdPathType.NonHardened).ConfigureAwait(false);

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
			foreach (var elem in await QueryOperationsPerAddressesAsync(addresses).ConfigureAwait(false))
			{
				operationsPerAddresses.Add(elem.Key, elem.Value);
				if (elem.Value.Count == 0) unusedKeyCount++;
			}
			WriteLine($"{operationsPerAddresses.Count} {hdPathType} keys are processed.");

			var startIndex = minUnusedKeys;
			while (unusedKeyCount < minUnusedKeys)
			{
				addresses = new HashSet<BitcoinAddress>();
				for (int i = startIndex; i < startIndex + minUnusedKeys; i++)
				{
					addresses.Add(safe.GetAddress(i, hdPathType.GetValueOrDefault()));
					//addresses.Add(FakeData.FakeSafe.GetAddress(i));
				}
				foreach (var elem in await QueryOperationsPerAddressesAsync(addresses).ConfigureAwait(false))
				{
					operationsPerAddresses.Add(elem.Key, elem.Value);
					if (elem.Value.Count == 0) unusedKeyCount++;
				}
				WriteLine($"{operationsPerAddresses.Count} {hdPathType} keys are processed.");
				startIndex += minUnusedKeys;
			}

			return operationsPerAddresses;
		}

		public static async Task<Dictionary<BitcoinAddress, List<BalanceOperation>>> QueryOperationsPerAddressesAsync(HashSet<BitcoinAddress> addresses)
		{
			var operationsPerAddresses = new Dictionary<BitcoinAddress, List<BalanceOperation>>(); ;
			var client = new QBitNinjaClient(Config.Network);

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

		private async static Task<IEnumerable<BalanceModel>> GetBalancesAsync(QBitNinjaClient client, IEnumerable<BitcoinAddress> addresses, bool unspentOnly)
		{
			var tasks = new HashSet<Task<BalanceModel>>();
			foreach (var dest in addresses)
			{
				var task = client.GetBalance(dest, unspentOnly: false);
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