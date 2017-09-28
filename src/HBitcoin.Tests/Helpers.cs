using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using DotNetTor.SocksPort;

namespace HBitcoin.Tests
{
    public static class Helpers
    {
		public static SocksPortHandler SocksPortHandler = new SocksPortHandler("127.0.0.1", socksPort: 9050);
		public static DotNetTor.ControlPort.Client ControlPortClient = new DotNetTor.ControlPort.Client("127.0.0.1", controlPort: 9051, password: "ILoveBitcoin21");

		public const string CommittedWalletsFolderPath = "../../../CommittedWallets";

        private static Height _prevHeight = Height.Unknown;
	    private static Height _prevHeaderHeight = Height.Unknown;

	    public static async Task ReportAsync(CancellationToken ctsToken, WalletJob walletJob)
	    {
		    while (true)
		    {
			    if (ctsToken.IsCancellationRequested) return;
			    try
			    {
				    await Task.Delay(1000, ctsToken).ContinueWith(t => { }).ConfigureAwait(false);

				    Height currHeaderHeight;
				    if(WalletJob.TryGetHeaderHeight(out currHeaderHeight))
				    {
					    // HEADERCHAIN
					    if(currHeaderHeight.Type == HeightType.Chain
							&& (_prevHeaderHeight == Height.Unknown || currHeaderHeight > _prevHeaderHeight))
					    {
						    Debug.WriteLine($"HeaderChain height: {currHeaderHeight}");
						    _prevHeaderHeight = currHeaderHeight;
					    }

					    // TRACKER
					    var currHeight = walletJob.BestHeight;
					    if(currHeight.Type == HeightType.Chain
							&& (_prevHeight == Height.Unknown || currHeight > _prevHeight))
					    {
						    Debug.WriteLine($"Tracker height: {currHeight} left: {currHeaderHeight.Value - currHeight.Value}");
						    _prevHeight = currHeight;
					    }
				    }
			    }
			    catch
			    {
				    // ignored
			    }
		    }
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

	    public static async Task<IEnumerable<BalanceModel>> GetBalancesAsync(QBitNinjaClient client, IEnumerable<BitcoinAddress> addresses, bool unspentOnly)
	    {
			//var tasks = new HashSet<Task<BalanceModel>>();
			//foreach (var dest in addresses)
			//{
			// var task = client.GetBalance(dest, unspentOnly);

			// tasks.Add(task);
			//}

			//await Task.WhenAll(tasks).ConfigureAwait(false);

			//var results = new HashSet<BalanceModel>();
			//foreach (var task in tasks)
			// results.Add(await task.ConfigureAwait(false));

			//return results;
			var results = new HashSet<BalanceModel>();
			foreach (var dest in addresses)
			{
				results.Add(await client.GetBalance(dest, unspentOnly).ConfigureAwait(false));
			}

			return results;
		}

	    public static void ReportFullHistory(WalletJob walletJob)
	    {
		    var history = walletJob.GetSafeHistory();
		    if (!history.Any())
		    {
			    Debug.WriteLine("Wallet has no history...");
			    return;
		    }

		    Debug.WriteLine("");
		    Debug.WriteLine("---------------------------------------------------------------------------");
		    Debug.WriteLine(@"Date			Amount		Confirmed	Transaction Id");
		    Debug.WriteLine("---------------------------------------------------------------------------");
			
		    foreach (var record in history)
		    {
			    Debug.WriteLine($@"{record.TimeStamp.DateTime}	{record.Amount}	{record.Confirmed}		{record.TransactionId}");
		    }
		    Debug.WriteLine("");
	    }
    }
}
