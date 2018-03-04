using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Services
{
	public class IndexBuilderService
	{
		public RPCClient RpcClient { get; }
		public string IndexFilePath { get; }
		public string Bech32UtxoSetFilePath { get; }

		private List<FilterModel> Index { get; }
		private AsyncLock IndexLock { get; }

		private Dictionary<OutPoint, Script> Bech32UtxoSet { get; }

		public Height StartingHeight // First possible bech32 transaction ever.
		{
			get
			{
				if (RpcClient.Network == Network.Main)
				{
					return new Height(481824);
				}
				else if (RpcClient.Network == Network.TestNet)
				{
					return new Height(828575);
				}
				else if (RpcClient.Network == Network.RegTest)
				{
					return new Height(0);
				}
				else
				{
					throw new NotSupportedException($"{RpcClient.Network} is not supported.");
				}
			}
		}
		
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		public IndexBuilderService(RPCClient rpc, string indexFilePath, string bech32UtxoSetFilePath)
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			IndexFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(indexFilePath), indexFilePath);
			Bech32UtxoSetFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(bech32UtxoSetFilePath), bech32UtxoSetFilePath);

			Bech32UtxoSet = new Dictionary<OutPoint, Script>();
			Index = new List<FilterModel>();
			IndexLock = new AsyncLock();

			_running = 0;

			var indexDir = Path.GetDirectoryName(IndexFilePath);
			Directory.CreateDirectory(indexDir);
			if (File.Exists(IndexFilePath))
			{
				if (RpcClient.Network == Network.RegTest)
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
			
			var utxoSetDir = Path.GetDirectoryName(bech32UtxoSetFilePath);
			Directory.CreateDirectory(utxoSetDir);
			if (File.Exists(bech32UtxoSetFilePath))
			{
				if (RpcClient.Network == Network.RegTest)
				{
					File.Delete(bech32UtxoSetFilePath); // RegTest is not a global ledger, better to delete it.
				}
				else
				{
					foreach (var line in File.ReadAllLines(Bech32UtxoSetFilePath))
					{
						var parts = line.Split(':');

						var txHash = new uint256(parts[0]);
						var nIn = int.Parse(parts[1]);
						var script = new Script(ByteHelpers.FromHex(parts[2]), true);
						Bech32UtxoSet.Add(new OutPoint(txHash, nIn), script);
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

						int height = StartingHeight.Value;
						uint256 prevHash = null;
						using (await IndexLock.LockAsync())
						{
							if (Index.Count != 0)
							{
								height = Index.Last().BlockHeight.Value + 1;
							}
						}

						Block block = null;
						try
						{
							block = await RpcClient.GetBlockAsync(height);
						}
						catch (RPCException) // if the block didn't come yet
						{
							// ToDO: If this happens, we should do `waitforblock` RPC instead of periodically asking.
							// In that case we must also make sure the correct error message comes.
							await Task.Delay(1000);
							continue;
						}

						if (prevHash != null)
						{
							// ToDo: IMPORTANT! The Bech32UtxoSet MUST be recovered to its previous state, too!
							if (prevHash != block.Header.HashPrevBlock) // reorg
							{
								using (await IndexLock.LockAsync())
								{
									Index.RemoveAt(Index.Count - 1);
								}

								// remove last line
								var lines = File.ReadAllLines(IndexFilePath);
								File.WriteAllLines(IndexFilePath, lines.Take(lines.Length - 1).ToArray());
								continue;
							}
						}

						var scripts = new HashSet<Script>();

						foreach (var tx in block.Transactions)
						{
							for (int i = 0; i < tx.Outputs.Count; i++)
							{
								var output = tx.Outputs[i];
								if (!output.ScriptPubKey.IsPayToScriptHash && output.ScriptPubKey.IsWitness)
								{
									Bech32UtxoSet.Add(new OutPoint(tx.GetHash(), i), output.ScriptPubKey);
									scripts.Add(output.ScriptPubKey);
								}
							}

							foreach (var input in tx.Inputs)
							{
								var found = Bech32UtxoSet.SingleOrDefault(x => x.Key == input.PrevOut);
								if (found.Key != default)
								{
									Bech32UtxoSet.Remove(input.PrevOut);
									scripts.Add(found.Value);
								}
							}
						}

						// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
						// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter 
						// is constructed.This ensures the key is deterministic while still varying from block to block.
						var key = block.GetHash().ToBytes().Take(16).ToArray();

						GolombRiceFilter filter = null;
						if (scripts.Count != 0)
						{
							filter = GolombRiceFilter.Build(key, scripts.Select(x => x.ToCompressedBytes()));
						}

						var filterModel = new FilterModel
						{
							BlockHash = block.GetHash(),
							BlockHeight = new Height(height),
							Filter = filter
						};

						await File.AppendAllLinesAsync(IndexFilePath, new[] { filterModel.ToLine() });
						using (await IndexLock.LockAsync())
						{
							Index.Add(filterModel);
						}
						if (File.Exists(Bech32UtxoSetFilePath))
						{
							File.Delete(Bech32UtxoSetFilePath);
						}
						await File.WriteAllLinesAsync(Bech32UtxoSetFilePath, Bech32UtxoSet
							.Select(entry => entry.Key.Hash + ":" + entry.Key.N + ":" + ByteHelpers.ToHex(entry.Value.ToCompressedBytes())));

						Logger.LogInfo<IndexBuilderService>($"Created filter for block: {height}.");
					}
					catch (Exception ex)
					{
						Logger.LogDebug<IndexBuilderService>(ex);
					}
				}
			});
		}

		public IEnumerable<string> GetFilters(uint256 bestKnownBlockHash)
		{
			using (IndexLock.Lock())
			{
				var found = false;
				foreach(var filter in Index)
				{
					if (found)
					{
						yield return filter.ToLine();
					}
					else
					{
						if (filter.BlockHash == bestKnownBlockHash)
						{
							found = true;
						}
					}					
				}
			}
		}

		public void Stop()
		{
			Interlocked.Exchange(ref _running, 0);
		}
	}
}
