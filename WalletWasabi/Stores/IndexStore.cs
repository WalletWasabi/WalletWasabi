using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// Manages to store the filters safely.
	/// </summary>
	public class IndexStore
	{
		private const string IndexFileName = "Index.dat";
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }
		private IoManager IndexFileManager { get; set; }
		public HashChain HashChain { get; private set; }

		private FilterModel StartingFilter { get; set; }
		private Height StartingHeight { get; set; }
		private List<FilterModel> Index { get; set; }
		private AsyncLock IndexLock { get; set; }

		public event EventHandler<FilterModel> Reorged;

		public event EventHandler<FilterModel> NewFilter;

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);

			Network = Guard.NotNull(nameof(network), network);
			var indexFilePath = Path.Combine(WorkFolderPath, IndexFileName);
			IndexFileManager = new IoManager(indexFilePath);

			StartingFilter = StartingFilters.GetStartingFilter(Network);
			StartingHeight = StartingFilters.GetStartingHeight(Network);

			Index = new List<FilterModel>();
			HashChain = new HashChain();

			IndexLock = new AsyncLock();

			using (await IndexLock.LockAsync())
			{
				using (await IndexFileManager.Mutex.LockAsync())
				{
					IoHelpers.EnsureDirectoryExists(WorkFolderPath);

					TryEnsureBackwardsCompatibility();

					if (Network == Network.RegTest)
					{
						IndexFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
					}

					if (!IndexFileManager.Exists())
					{
						await IndexFileManager.WriteAllLinesAsync(new[] { StartingFilter.ToHeightlessLine() });
					}

					var height = StartingHeight;
					try
					{
						foreach (var line in await IndexFileManager.ReadAllLinesAsync())
						{
							var filter = FilterModel.FromHeightlessLine(line, height);
							height++;
							Index.Add(filter);
							HashChain.AddOrReplace(filter.BlockHeight.Value, filter.BlockHash);
						}
					}
					catch (FormatException)
					{
						// We found a corrupted entry. Stop here.
						// Fix the currupted file.
						await IndexFileManager.WriteAllLinesAsync(Index.Select(x => x.ToHeightlessLine()));
					}
				}
			}
		}

		private void TryEnsureBackwardsCompatibility()
		{
			try
			{
				// Before Wasabi 1.1.5
				var oldIndexFilepath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), $"Index{Network}.dat");
				IndexFileManager.TryReplaceMeWith(oldIndexFilepath);
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IndexStore>($"Backwards compatibility couldn't be ensured. Exception: {ex.ToString()}");
			}
		}

		public async Task AddNewFiltersAsync(params FilterModel[] filters)
		{
			foreach (var filter in filters)
			{
				using (await IndexLock.LockAsync())
				{
					Index.Add(filter);
					HashChain.AddOrReplace(filter.BlockHeight.Value, filter.BlockHash);
				}

				NewFilter?.Invoke(this, filter); // Event always outside the lock.
			}
		}

		public async Task<FilterModel> RemoveLastFilterAsync()
		{
			FilterModel filter = null;

			using (await IndexLock.LockAsync())
			{
				filter = Index.Last();
				Index.RemoveLast();
				HashChain.RemoveLast();
			}

			Reorged?.Invoke(this, filter);

			return filter;
		}

		private int _throttleId;

		/// <summary>
		/// It'll LogError the exceptions.
		/// If cancelled, it'll LogTrace the exception.
		/// </summary>
		public async Task TryCommitToFileAsync(TimeSpan throttle, CancellationToken cancel)
		{
			try
			{
				// If throttle is requested, then throttle.
				if (throttle != TimeSpan.Zero)
				{
					// Increment the throttle ID and remember the incremented value.
					int incremented = Interlocked.Increment(ref _throttleId);
					await Task.Delay(throttle, cancel);

					// If the _throttleId is still the incremented value, then I am the latest CommitToFileAsync request.
					//	In this case I want to make the _throttledId 0 and go ahead and do the writeline.
					// If the _throttledId is not the incremented value anymore then I am not the latest request here,
					//	So just return, the latest request will do the file write in its own time.
					if (Interlocked.CompareExchange(ref _throttleId, 0, incremented) != incremented)
					{
						return;
					}
				}
				else
				{
					Interlocked.Exchange(ref _throttleId, 0); // So to notified the currently throttled threads that they don't have to run.
				}

				using (await IndexFileManager.Mutex.LockAsync(cancel))
				using (await IndexLock.LockAsync(cancel))
				{
					await IndexFileManager.WriteAllLinesAsync(Index.Select(x => x.ToHeightlessLine())); // Don't feed the cancellationToken here I always want this to finish running for safety.
				}
			}
			catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
			{
				Logger.LogTrace<IndexStore>(ex);
			}
			catch (Exception ex)
			{
				Logger.LogError<IndexStore>(ex);
			}
		}

		public async Task<Height?> TryGetHeightAsync(uint256 blockHash)
		{
			Height? ret = null;

			using (await IndexLock.LockAsync())
			{
				ret = Index.FirstOrDefault(x => x.BlockHash == blockHash)?.BlockHeight;
			}
			return ret;
		}

		public async Task<IEnumerable<FilterModel>> GetFiltersAsync()
		{
			List<FilterModel> ret = null;

			using (await IndexLock.LockAsync())
			{
				ret = Index.ToList();
			}
			return ret;
		}

		public async Task<int> CountFiltersAsync()
		{
			int ret;

			using (await IndexLock.LockAsync())
			{
				ret = Index.Count;
			}
			return ret;
		}
	}
}
