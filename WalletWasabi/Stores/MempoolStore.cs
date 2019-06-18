using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// Manages to store the mempool safely.
	/// </summary>
	public class MempoolStore
	{
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }
		private IoManager MempoolFileManager { get; set; }
		private HashSet<SmartTransaction> Mempool { get; set; }
		private AsyncLock MempoolLock { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			var mempoolFilePath = Path.Combine(WorkFolderPath, "Mempool.dat");
			MempoolFileManager = new IoManager(mempoolFilePath);

			MempoolLock = new AsyncLock();

			using (await MempoolLock.LockAsync())
			using (await MempoolFileManager.Mutex.LockAsync())
			{
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				await TryEnsureBackwardsCompatibilityAsync();

				if (Network == Network.RegTest)
				{
					MempoolFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
				}

				await InitializeMempoolAsync();
			}
		}

		private async Task InitializeMempoolAsync()
		{
			try
			{
				Height height = StartingHeight;

				if (MatureIndexFileManager.Exists())
				{
					using (var sr = MatureIndexFileManager.OpenText())
					{
						if (!sr.EndOfStream)
						{
							var lineTask = sr.ReadLineAsync();
							string line = null;
							while (lineTask != null)
							{
								if (line is null)
								{
									line = await lineTask;
								}

								lineTask = sr.EndOfStream ? null : sr.ReadLineAsync();

								ProcessLine(height, line, enqueue: false);
								height++;

								line = null;
							}
						}
					}
				}

				if (ImmatureIndexFileManager.Exists())
				{
					foreach (var line in await ImmatureIndexFileManager.ReadAllLinesAsync()) // We can load ImmatureIndexFileManager to the memory, no problem.
					{
						ProcessLine(height, line, enqueue: true);
						height++;
					}
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Don't try to autocorrect, because the internal data structures are throwing events those may confuse the consumers of those events.
				Logger.LogError<IndexStore>("An index file got corrupted. Deleting index files...");
				MatureIndexFileManager.DeleteMe();
				ImmatureIndexFileManager.DeleteMe();
				throw;
			}
		}

		private async Task TryEnsureBackwardsCompatibilityAsync()
		{
			try
			{
				// Before Wasabi 1.1.6
				var oldTransactionsFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Transactions", Network.ToString());

				foreach (var txFile in Directory.EnumerateFiles(oldTransactionsFolderPath))
				{
					await MempoolFileManager.WriteAllLinesAsync(Mempool.OrderBy(x => x.Height).ThenBy(x => x.FirstSeenIfMemPoolTime).Select(x => x.ToLine()));

					File.Delete(txFile);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IndexStore>($"Backwards compatibility couldn't be ensured. Exception: {ex.ToString()}");
			}
		}
	}
}
