using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.TorSocks5;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Services
{
    public class IndexDownloader
    {
		public Network Network { get; }

		public TorHttpClient Client { get; }

		public string IndexFilePath { get; }
		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }
		
		public static Height GetStartingHeight(Network network) => IndexBuilderService.GetStartingHeight(network);
		public Height StartingHeight => GetStartingHeight(Network);

		public static FilterModel GetStartingFilter(Network network)
		{
			if (network == Network.Main)
			{
				return FilterModel.FromLine("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893:2:43:11288322B003", GetStartingHeight(network));
			}
			else if (network == Network.TestNet)
			{
				return FilterModel.FromLine("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:1:21:6E081E", GetStartingHeight(network));
			}
			else if (network == Network.RegTest)
			{
				return FilterModel.FromLine("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", GetStartingHeight(network));
			}
			else
			{
				throw new NotSupportedException($"{network} is not supported.");
			}
		}
		public FilterModel StartingFilter => GetStartingFilter(Network);

		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		public IndexDownloader(Network network, string indexFilePath, Uri indexHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			Client = new TorHttpClient(indexHostUri, torSocks5EndPoint, isolateStream: false);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);

			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			_running = 0;

			var indexDir = Path.GetDirectoryName(IndexFilePath);
			Directory.CreateDirectory(indexDir);
			if (File.Exists(IndexFilePath))
			{
				if (Network == Network.RegTest)
				{
					File.Delete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
				}
				else
				{
					int height = StartingHeight.Value;
					foreach (var line in File.ReadAllLines(IndexFilePath))
					{
						var filter = FilterModel.FromLine(line, new Height(height));
						height++;
						Index.Add(filter);
					}
				}
			}
		}

		public void Syncronize()
		{
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				while (IsRunning)
				{
					try
					{
						// If stop was requested return.
						if (IsRunning == false) return;

						//int height = StartingHeight.Value;
						//uint256 prevHash = null;
						//using (await IndexLock.LockAsync())
						//{
						//	if (Index.Count != 0)
						//	{
						//		height = Index.Last().BlockHeight.Value + 1;
						//		prevHash = Index.Last().BlockHash;
						//	}
						//}

						//Block block = null;
						//try
						//{
						//	block = await RpcClient.GetBlockAsync(height);
						//}
						//catch (RPCException) // if the block didn't come yet
						//{
						//	// ToDO: If this happens, we should do `waitforblock` RPC instead of periodically asking.
						//	// In that case we must also make sure the correct error message comes.
						//	await Task.Delay(1000);
						//	continue;
						//}

						//if (prevHash != null)
						//{
						//	// In case of reorg:
						//	if (prevHash != block.Header.HashPrevBlock)
						//	{
						//		Logger.LogInfo<IndexBuilderService>($"REORG Invalid Block: {prevHash}");
						//		// 1. Rollback index
						//		using (await IndexLock.LockAsync())
						//		{
						//			Index.RemoveAt(Index.Count - 1);
						//		}

						//		// 2. Serialize Index. (Remove last line.)
						//		var lines = File.ReadAllLines(IndexFilePath);
						//		File.WriteAllLines(IndexFilePath, lines.Take(lines.Length - 1).ToArray());

						//		// 3. Rollback Bech32UtxoSet
						//		Bech32UtxoSetHistory.Rollback(Bech32UtxoSet); // The Bech32UtxoSet MUST be recovered to its previous state.

						//		// 4. Serialize Bech32UtxoSet.
						//		await File.WriteAllLinesAsync(Bech32UtxoSetFilePath, Bech32UtxoSet
						//			.Select(entry => entry.Key.Hash + ":" + entry.Key.N + ":" + ByteHelpers.ToHex(entry.Value.ToCompressedBytes())));

						//		// 5. Skip the current block.
						//		continue;
						//	}
						//}

						//Bech32UtxoSetHistory.ClearActionHistory(); //reset history.

						//var scripts = new HashSet<Script>();

						//foreach (var tx in block.Transactions)
						//{
						//	for (int i = 0; i < tx.Outputs.Count; i++)
						//	{
						//		var output = tx.Outputs[i];
						//		if (!output.ScriptPubKey.IsPayToScriptHash && output.ScriptPubKey.IsWitness)
						//		{
						//			var outpoint = new OutPoint(tx.GetHash(), i);
						//			Bech32UtxoSet.Add(outpoint, output.ScriptPubKey);
						//			Bech32UtxoSetHistory.StoreAction(ActionHistoryHelper.Operation.Add, outpoint, output.ScriptPubKey);
						//			scripts.Add(output.ScriptPubKey);
						//		}
						//	}

						//	foreach (var input in tx.Inputs)
						//	{
						//		var found = Bech32UtxoSet.SingleOrDefault(x => x.Key == input.PrevOut);
						//		if (found.Key != default)
						//		{
						//			Script val = Bech32UtxoSet[input.PrevOut];
						//			Bech32UtxoSet.Remove(input.PrevOut);
						//			Bech32UtxoSetHistory.StoreAction(ActionHistoryHelper.Operation.Remove, input.PrevOut, val);
						//			scripts.Add(found.Value);
						//		}
						//	}
						//}

						//// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
						//// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter 
						//// is constructed.This ensures the key is deterministic while still varying from block to block.
						//var key = block.GetHash().ToBytes().Take(16).ToArray();

						//GolombRiceFilter filter = null;
						//if (scripts.Count != 0)
						//{
						//	filter = GolombRiceFilter.Build(key, scripts.Select(x => x.ToCompressedBytes()));
						//}

						//var filterModel = new FilterModel
						//{
						//	BlockHash = block.GetHash(),
						//	BlockHeight = new Height(height),
						//	Filter = filter
						//};

						//await File.AppendAllLinesAsync(IndexFilePath, new[] { filterModel.ToLine() });
						//using (await IndexLock.LockAsync())
						//{
						//	Index.Add(filterModel);
						//}
						//if (File.Exists(Bech32UtxoSetFilePath))
						//{
						//	File.Delete(Bech32UtxoSetFilePath);
						//}
						//await File.WriteAllLinesAsync(Bech32UtxoSetFilePath, Bech32UtxoSet
						//	.Select(entry => entry.Key.Hash + ":" + entry.Key.N + ":" + ByteHelpers.ToHex(entry.Value.ToCompressedBytes())));

						//Logger.LogInfo<IndexBuilderService>($"Created filter for block: {height}.");
					}
					catch (Exception ex)
					{
						Logger.LogDebug<IndexDownloader>(ex);
					}
					finally
					{
						await Task.Delay(TimeSpan.FromSeconds(30)); // Ask for new index every 30 seconds.
					}
				}
			});
		}

		public void Stop()
		{
			Interlocked.Exchange(ref _running, 0);
		}
	}
}
