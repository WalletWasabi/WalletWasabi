using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using HBitcoin.Models;
using NBitcoin;

namespace HBitcoin.FullBlockSpv
{
	public class Tracker
    {
		#region Members

		public Network Network { get; private set; }
		public ConcurrentHashSet<SmartMerkleBlock> MerkleChain { get; } = new ConcurrentHashSet<SmartMerkleBlock>();

	    /// <summary>
	    /// 
	    /// </summary>
	    /// <param name="scriptPubKey"></param>
	    /// <param name="receivedTransactions">int: block height</param>
	    /// <param name="spentTransactions">int: block height</param>
	    /// <returns></returns>
	    public bool TryFindConfirmedTransactions(Script scriptPubKey, out ConcurrentHashSet<SmartTransaction> receivedTransactions, out ConcurrentHashSet<SmartTransaction> spentTransactions)
	    {
		    var found = false;
		    receivedTransactions = new ConcurrentHashSet<SmartTransaction>();
			spentTransactions = new ConcurrentHashSet<SmartTransaction>();
			
			foreach(var tx in TrackedTransactions.Where(x=>x.Confirmed))
			{
				// if already has that tx continue
				if(receivedTransactions.Any(x => x.GetHash() == tx.GetHash()))
					continue;

				foreach(var output in tx.Transaction.Outputs)
				{
					if(output.ScriptPubKey.Equals(scriptPubKey))
					{
						receivedTransactions.Add(tx);
						found = true;
					}
				}
			}

		    if(found)
		    {
			    foreach(var tx in TrackedTransactions.Where(x => x.Confirmed))
			    {
				    // if already has that tx continue
				    if(spentTransactions.Any(x => x.GetHash() == tx.GetHash()))
					    continue;

				    foreach(var input in tx.Transaction.Inputs)
				    {
					    if(receivedTransactions.Select(x => x.GetHash()).Contains(input.PrevOut.Hash))
					    {
						    spentTransactions.Add(tx);
						    found = true;
					    }
				    }
			    }
		    }

		    return found;
	    }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="scriptPubKey"></param>
		/// <returns>if never had any money on it</returns>
		public bool IsClean(Script scriptPubKey) => TrackedTransactions.All(tx => !tx.Transaction.Outputs.Any(output => output.ScriptPubKey.Equals(scriptPubKey)));

	    public ConcurrentObservableHashSet<SmartTransaction> TrackedTransactions { get; }
			= new ConcurrentObservableHashSet<SmartTransaction>();
		public ConcurrentHashSet<Script> TrackedScriptPubKeys { get; }
			= new ConcurrentHashSet<Script>();

	    public readonly UnprocessedBlockBuffer UnprocessedBlockBuffer = new UnprocessedBlockBuffer();

		private Height _bestHeight = Height.Unknown;
		public Height BestHeight
		{
			get { return _bestHeight; }
			private set
			{
				if (_bestHeight == value) return;
				_bestHeight = value;
				OnBestHeightChanged();
			}
		}
		public event EventHandler BestHeightChanged;
		private void OnBestHeightChanged() => BestHeightChanged?.Invoke(this, EventArgs.Empty);

		public int BlockCount => MerkleChain.Count;

		#endregion

		#region Constructors

		private Tracker()
		{
		}
		public Tracker(Network network)
		{
			Network = network;
			UnprocessedBlockBuffer.HaveBlocks += UnprocessedBlockBuffer_HaveBlocks;
		}

		#endregion
        
		public void ReorgOne()
		{
			UnprocessedBlockBuffer.Clear();
			// remove the last block
			if (MerkleChain.Count != 0)
			{
				var bestBlock = MerkleChain.Max();
				if (MerkleChain.TryRemove(bestBlock))
				{
					List<SmartTransaction> affectedTxs = TrackedTransactions.Where(x => x.Height == bestBlock.Height).Select(x=>x).ToList();
					foreach (var tx in affectedTxs)
					{
						TrackedTransactions.TryRemove(tx);
						// add it back as a mempool transaction, it'll drop out anyway
						TrackedTransactions.TryAdd(new SmartTransaction(tx.Transaction, Height.MemPool));
					}
					if (MerkleChain.Count != 0)
					{
						BestHeight = MerkleChain.Max().Height;
					}
					else
					{
						BestHeight = Height.Unknown;
					}
				}
			}
		}

	    public void AddOrReplaceBlock(Height height, Block block)
	    {
		    UnprocessedBlockBuffer.TryAddOrReplace(height, block);
	    }

		#region TransactionProcessing

		/// <returns>if processed it transaction</returns>
		public bool ProcessTransaction(SmartTransaction transaction)
	    {
			// 1. If already tracking can we update it?
		    var found = TrackedTransactions.FirstOrDefault(x => x == transaction);
			if (found != default(SmartTransaction))
		    {
				// if in a lower level don't track
			    if(found.Height.Type <= transaction.Height.Type)
				    return false;
			    else
			    {
					// else update
					TrackedTransactions.TryRemove(transaction);
					TrackedTransactions.TryAdd(transaction);
					return true;
				}
		    }

			// 2. If this transaction arrived to any of our scriptpubkey track it!
			if (transaction.Transaction.Outputs.Any(output => TrackedScriptPubKeys.Contains(output.ScriptPubKey)))
			{
				TrackedTransactions.TryAdd(transaction);
				return true;
			}

			// 3. If this transaction spends any of our scriptpubkeys track it!
		    if(transaction.Transaction.Inputs.Any(input => TrackedTransactions
			    .Where(ttx => ttx.GetHash() == input.PrevOut.Hash)
			    .Any(ttx => TrackedScriptPubKeys
				    .Contains(ttx.Transaction.Outputs[input.PrevOut.N].ScriptPubKey))))
		    {
			    TrackedTransactions.TryAdd(transaction);
			    return true;
		    }

			// if got so far we are not interested
			return false;
		}

		/// <returns>transactions it processed, empty if not processed any</returns>
		private HashSet<SmartTransaction> ProcessTransactions(IEnumerable<Transaction> transactions, Height height)
		{
			var processedTransactions = new HashSet<SmartTransaction>();
			try
			{
				// Process all transactions
				foreach(var tx in transactions)
				{
					var smartTx = new SmartTransaction(tx, height);
					if(ProcessTransaction(smartTx))
					{
						processedTransactions.Add(smartTx);
					}
				}

				// If processed any then do it again recursively until zero new is processed
				if(processedTransactions.Count > 0)
				{
					var newlyProcessedTransactions = ProcessTransactions(transactions, height);
					foreach(var ptx in newlyProcessedTransactions)
					{
						processedTransactions.Add(ptx);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Ignoring {nameof(ProcessBlock)} exception at height {height}:");
				Debug.WriteLine(ex);
			}

			return processedTransactions;
		}

		/// <returns>transactions it processed, empty if not processed any</returns>
		private HashSet<SmartTransaction> ProcessBlock(Height height, Block block)
		{
			var foundTransactions = ProcessTransactions(block.Transactions, height);

			var smartMerkleBlock = new SmartMerkleBlock(height, block, foundTransactions.Select(x => x.GetHash()).ToArray());

			var sameHeights = MerkleChain.Where(x => x.Height == height);
			foreach (var elem in sameHeights)
			{
				MerkleChain.TryRemove(elem);
			}

			MerkleChain.Add(smartMerkleBlock);
			if (BestHeight < height || BestHeight == Height.Unknown)
			{
				BestHeight = height;
			}

			return foundTransactions;
		}

		#endregion

		private void UnprocessedBlockBuffer_HaveBlocks(object sender, EventArgs e)
		{
            while (UnprocessedBlockBuffer.TryGetAndRemoveOldest(out Height height, out Block block))
            {
                ProcessBlock(height, block);
            }
        }

		#region Saving

		private readonly SemaphoreSlim Saving = new SemaphoreSlim(1, 1);

	    private const string TrackedScriptPubKeysFileName = "TrackedScriptPubKeys.dat";
	    private const string TrackedTransactionsFileName = "TrackedTransactions.dat";
	    private const string MerkleChainFileName = "MerkleChain.dat";

		private static readonly byte[] blockSep = new byte[] { 0x10, 0x1A, 0x7B, 0x23, 0x5D, 0x12, 0x7D };
		public async Task SaveAsync(string trackerFolderPath)
		{
			await Saving.WaitAsync().ConfigureAwait(false);
			try
			{
				if (TrackedScriptPubKeys.Count > 0 || TrackedTransactions.Count > 0 || MerkleChain.Count > 0)
				{
					Directory.CreateDirectory(trackerFolderPath);
				}

				if (TrackedScriptPubKeys.Count > 0)
				{
					File.WriteAllLines(
						Path.Combine(trackerFolderPath, TrackedScriptPubKeysFileName),
						TrackedScriptPubKeys.Select(x => x.ToString()));
				}

				if (TrackedTransactions.Count > 0)
				{
					File.WriteAllLines(
						Path.Combine(trackerFolderPath, TrackedTransactionsFileName),
						TrackedTransactions
						.Select(x => $"{x.Transaction.ToHex()}:{x.Height}"));
				}

				if (MerkleChain.Count > 0)
				{
					var path = Path.Combine(trackerFolderPath, MerkleChainFileName);

					if(File.Exists(path))
					{
						const string backupName = MerkleChainFileName + "_backup";
						var backupPath = Path.Combine(trackerFolderPath, backupName);
						File.Copy(path, backupPath, overwrite: true);
						File.Delete(path);
					}

					using(FileStream stream = File.OpenWrite(path))
					{
						var toFile = MerkleChain.First().ToBytes();
						await stream.WriteAsync(toFile, 0, toFile.Length).ConfigureAwait(false);
						foreach(var block in MerkleChain.Skip(1))
						{
							await stream.WriteAsync(blockSep, 0, blockSep.Length).ConfigureAwait(false);
							var blockBytes = block.ToBytes();
							await stream.WriteAsync(blockBytes, 0, blockBytes.Length).ConfigureAwait(false);
						}
					}
				}
			}
			finally
			{
				Saving.Release();
			}
		}

		public async Task LoadAsync(string trackerFolderPath)
		{
			await Saving.WaitAsync().ConfigureAwait(false);
			try
			{
				if (!Directory.Exists(trackerFolderPath))
					throw new DirectoryNotFoundException($"No Blockchain found at {trackerFolderPath}");

				var tspb = Path.Combine(trackerFolderPath, TrackedScriptPubKeysFileName);
				if (File.Exists(tspb) && new FileInfo(tspb).Length != 0)
				{
					foreach (var line in File.ReadAllLines(tspb))
					{
						TrackedScriptPubKeys.Add(new Script(line));
					}
				}

				var tt = Path.Combine(trackerFolderPath, TrackedTransactionsFileName);
				if (File.Exists(tt) && new FileInfo(tt).Length != 0)
				{
					foreach (var line in File.ReadAllLines(tt))
					{
						var pieces = line.Split(':');
						ProcessTransaction(new SmartTransaction(new Transaction(pieces[0]), new Height(pieces[1])));
					}
				}

				var pbc = Path.Combine(trackerFolderPath, MerkleChainFileName);
				if (File.Exists(pbc) && new FileInfo(pbc).Length != 0)
				{
					foreach (var block in Util.Separate(File.ReadAllBytes(pbc), blockSep))
					{
						try
						{
							SmartMerkleBlock smartMerkleBlock = SmartMerkleBlock.FromBytes(block);
							MerkleChain.Add(smartMerkleBlock);
						}
						catch(EndOfStreamException)
						{
							// Some corruption is fine, the software will self correct and save the right data
						}
					}
					if (MerkleChain.Count != 0)
					{
						BestHeight = MerkleChain.Max().Height;
					}
				}
			}
			finally
			{
				Saving.Release();
			}
		}

		#endregion
	}
}
