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
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }
		private IoManager MatureIndexFileManager { get; set; }
		private IoManager ImmatureIndexFileManager { get; set; }
		public HashChain HashChain { get; private set; }

		private FilterModel StartingFilter { get; set; }
		private Height StartingHeight { get; set; }
		private List<FilterModel> ImmatureFilters { get; set; }
		private AsyncLock IndexLock { get; set; }

		public event EventHandler<FilterModel> Reorged;

		public event EventHandler<FilterModel> NewFilter;

		public async Task InitializeAsync(string workFolderPath, Network network, HashChain hashChain)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			HashChain = Guard.NotNull(nameof(hashChain), hashChain);
			var indexFilePath = Path.Combine(WorkFolderPath, "MatureIndex.dat");
			MatureIndexFileManager = new IoManager(indexFilePath, digestRandomIndex: -1);
			var immatureIndexFilePath = Path.Combine(WorkFolderPath, "ImmatureIndex.dat");
			ImmatureIndexFileManager = new IoManager(immatureIndexFilePath, digestRandomIndex: -1);

			StartingFilter = StartingFilters.GetStartingFilter(Network);
			StartingHeight = StartingFilters.GetStartingHeight(Network);

			ImmatureFilters = new List<FilterModel>(150);

			IndexLock = new AsyncLock();

			using (await IndexLock.LockAsync())
			{
				using (await MatureIndexFileManager.Mutex.LockAsync())
				using (await ImmatureIndexFileManager.Mutex.LockAsync())
				{
					IoHelpers.EnsureDirectoryExists(WorkFolderPath);

					await TryEnsureBackwardsCompatibilityAsync();

					if (Network == Network.RegTest)
					{
						MatureIndexFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
						ImmatureIndexFileManager.DeleteMe();
					}

					if (!MatureIndexFileManager.Exists())
					{
						await MatureIndexFileManager.WriteAllLinesAsync(new[] { StartingFilter.ToHeightlessLine() });
					}

					await InitializeFiltersAsync();
				}
			}
		}

		private async Task InitializeFiltersAsync()
		{
			try
			{
				Height height = StartingHeight;

				if (MatureIndexFileManager.Exists())
				{
					using (var sr = MatureIndexFileManager.OpenText(16384))
					{
						while (!sr.EndOfStream)
						{
							var line = await sr.ReadLineAsync();
							ProcessLine(height, line, enqueue: false);
							height++;
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

		private void ProcessLine(Height height, string line, bool enqueue)
		{
			var filter = FilterModel.FromHeightlessLine(line, height);
			ProcessFilter(filter, enqueue);
		}

		private void ProcessFilter(FilterModel filter, bool enqueue)
		{
			if (enqueue)
			{
				ImmatureFilters.Add(filter);
			}
			HashChain.AddOrReplace(filter.BlockHeight.Value, filter.BlockHash);
		}

		private async Task TryEnsureBackwardsCompatibilityAsync()
		{
			try
			{
				// Before Wasabi 1.1.5
				var oldIndexFilepath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), $"Index{Network}.dat");

				if (File.Exists(oldIndexFilepath))
				{
					string[] allLines = await File.ReadAllLinesAsync(oldIndexFilepath);
					var matureLines = allLines.SkipLast(100);
					var immatureLines = allLines.TakeLast(100);

					await MatureIndexFileManager.WriteAllLinesAsync(matureLines);
					await ImmatureIndexFileManager.WriteAllLinesAsync(immatureLines);

					File.Delete(oldIndexFilepath);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IndexStore>($"Backwards compatibility couldn't be ensured. Exception: {ex.ToString()}");
			}
		}

		public async Task AddNewFiltersAsync(IEnumerable<FilterModel> filters, CancellationToken cancel)
		{
			foreach (var filter in filters)
			{
				using (await IndexLock.LockAsync())
				{
					ProcessFilter(filter, enqueue: true);
				}

				NewFilter?.Invoke(this, filter); // Event always outside the lock.
			}

			_ = TryCommitToFileAsync(TimeSpan.FromSeconds(3), cancel);
		}

		public async Task<FilterModel> RemoveLastFilterAsync(CancellationToken cancel)
		{
			FilterModel filter = null;

			using (await IndexLock.LockAsync())
			{
				filter = ImmatureFilters.Last();
				ImmatureFilters.RemoveLast();
				if (HashChain.TipHeight != filter.BlockHeight.Value)
				{
					throw new InvalidOperationException("HashChain and ImmatureFilters are not in sync.");
				}
				HashChain.RemoveLast();
			}

			Reorged?.Invoke(this, filter);

			_ = TryCommitToFileAsync(TimeSpan.FromSeconds(3), cancel);

			return filter;
		}

		private int _throttleId;

		/// <summary>
		/// It'll LogError the exceptions.
		/// If cancelled, it'll LogTrace the exception.
		/// </summary>
		private async Task TryCommitToFileAsync(TimeSpan throttle, CancellationToken cancel)
		{
			try
			{
				// If throttle is requested, then throttle.
				if (throttle != TimeSpan.Zero)
				{
					// Increment the throttle ID and remember the incremented value.
					int incremented = Interlocked.Increment(ref _throttleId);
					if (incremented < 21)
					{
						await Task.Delay(throttle, cancel);
					}

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

				using (await MatureIndexFileManager.Mutex.LockAsync(cancel))
				using (await ImmatureIndexFileManager.Mutex.LockAsync(cancel))
				using (await IndexLock.LockAsync(cancel))
				{
					// Don't feed the cancellationToken here I always want this to finish running for safety.
					var currentImmatureLines = ImmatureFilters.Select(x => x.ToHeightlessLine());
					var matureLinesToAppend = currentImmatureLines.SkipLast(100);
					var immatureLines = currentImmatureLines.TakeLast(100);
					await MatureIndexFileManager.AppendAllLinesAsync(matureLinesToAppend);
					await ImmatureIndexFileManager.WriteAllLinesAsync(immatureLines);
					while (ImmatureFilters.Count > 100)
					{
						ImmatureFilters.RemoveFirst();
					}
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

		public async Task<IEnumerable<FilterModel>> GetFiltersAsync(Height from, Height to, CancellationToken cancel)
		{
			List<FilterModel> ret = null;

			using (await IndexLock.LockAsync(cancel))
			{
				if (ImmatureFilters.Any(x => x.BlockHeight == from))
				{
					ret = ImmatureFilters.Where(x => x.BlockHeight >= from && x.BlockHeight <= to).ToList();
				}
				else
				{
					ret = new List<FilterModel>();
					Height height = StartingHeight;
					var firstImmatureHeight = ImmatureFilters.FirstOrDefault()?.BlockHeight;

					using (await MatureIndexFileManager.Mutex.LockAsync(cancel))
					{
						if (MatureIndexFileManager.Exists())
						{
							using (var sr = MatureIndexFileManager.OpenText(16384))
							{
								while (!sr.EndOfStream)
								{
									var line = await sr.ReadLineAsync();

									if (firstImmatureHeight == height)
									{
										break; // Let's use our the immature filters from here on. The content is the same, just someone else modified the file.
									}

									var filter = FilterModel.FromHeightlessLine(line, height);

									if (filter.BlockHeight >= from && filter.BlockHeight <= to)
									{
										ret.Add(filter);
									}

									height++;

									cancel.ThrowIfCancellationRequested();
								}
							}
						}

						foreach (FilterModel filter in ImmatureFilters)
						{
							ret.Add(filter);
						}
					}
				}
			}

			return ret;
		}

		public async Task UseFilterModelsAsync(Action<FilterModel, CancellationToken> todo, CancellationToken cancel)
		{
			using (await MatureIndexFileManager.Mutex.LockAsync(cancel))
			using (await IndexLock.LockAsync(cancel))
			{
				Height height = StartingHeight;
				var firstImmatureHeight = ImmatureFilters.FirstOrDefault()?.BlockHeight;

				if (MatureIndexFileManager.Exists())
				{
					using (var sr = MatureIndexFileManager.OpenText(16384))
					{
						while (!sr.EndOfStream)
						{
							var line = await sr.ReadLineAsync();

							if (firstImmatureHeight == height)
							{
								break; // Let's use our the immature filters from here on. The content is the same, just someone else modified the file.
							}

							var filter = FilterModel.FromHeightlessLine(line, height);

							todo(filter, cancel);
							height++;

							cancel.ThrowIfCancellationRequested();
						}
					}
				}

				foreach (FilterModel filter in ImmatureFilters)
				{
					todo(filter, cancel);
				}
			}
		}
	}
}
