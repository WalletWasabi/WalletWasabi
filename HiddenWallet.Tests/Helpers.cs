﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using DotNetTor.SocksPort;
using HiddenWallet.FullSpv;
using HiddenWallet.Models;
using HiddenWallet.KeyManagement;

namespace HiddenWallet.Tests
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
				    await Task.Delay(1000, ctsToken).ContinueWith(t => { });

                    var result = await walletJob.TryGetHeaderHeightAsync();
                    var currHeaderHeight = result.Height;
                    if (result.Success)
                    {
                        // HEADERCHAIN
                        if (currHeaderHeight.Type == HeightType.Chain
                            && (_prevHeaderHeight == Height.Unknown || currHeaderHeight > _prevHeaderHeight))
                        {
                            Debug.WriteLine($"HeaderChain height: {currHeaderHeight}");
                            _prevHeaderHeight = currHeaderHeight;
                        }

                        // TRACKER
                        var currHeight = await walletJob.GetBestHeightAsync();
                        if (currHeight.Type == HeightType.Chain
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
		        foreach(var scriptPubKey in safe.GetFirstNScriptPubKey(addressType, minUnusedKeys, hdPathType.GetValueOrDefault()))
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
		    var operationsPerScriptPubKeysAsync = new Dictionary<Script, List<BalanceOperation>>();

		    var scriptPubKeyList = scriptPubKeys.ToList();
		    var balanceModelList = new List<BalanceModel>();

		    foreach (var balance in await GetBalancesAsync(client, scriptPubKeyList, unspentOnly: false))
		    {
			    balanceModelList.Add(balance);
		    }

		    for (var i = 0; i < balanceModelList.Count; i++)
		    {
				operationsPerScriptPubKeysAsync.Add(scriptPubKeyList[i], balanceModelList[i].Operations);
		    }

		    return operationsPerScriptPubKeysAsync;
	    }

	    public static async Task<IEnumerable<BalanceModel>> GetBalancesAsync(QBitNinjaClient client, IEnumerable<Script> scriptPubKeys, bool unspentOnly)
	    {
			var results = new HashSet<BalanceModel>();
			foreach (var dest in scriptPubKeys)
			{
				results.Add(await client.GetBalance(dest, unspentOnly));
			}

			return results;
		}

	    public static async Task ReportFullHistoryAsync(WalletJob walletJob)
	    {
		    var history = await walletJob.GetSafeHistoryAsync();
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
