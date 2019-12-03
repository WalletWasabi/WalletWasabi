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
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
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
		private DigestableSafeMutexIoManager MatureIndexFileManager { get; set; }
		private DigestableSafeMutexIoManager ImmatureIndexFileManager { get; set; }
		public SmartHeaderChain SmartHeaderChain { get; private set; }

		private FilterModel StartingFilter { get; set; }
		private uint StartingHeight { get; set; }
		private List<FilterModel> ImmatureFilters { get; set; }
		private AsyncLock IndexLock { get; set; }

		public event EventHandler<FilterModel> Reorged;

		public event EventHandler<FilterModel> NewFilter;

		public async Task InitializeAsync(string workFolderPath, Network network, SmartHeaderChain hashChain)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				Network = Guard.NotNull(nameof(network), network);
				SmartHeaderChain = Guard.NotNull(nameof(hashChain), hashChain);
				var indexFilePath = Path.Combine(WorkFolderPath, "MatureIndex.dat");
				MatureIndexFileManager = new DigestableSafeMutexIoManager(indexFilePath, digestRandomIndex: -1);
				var immatureIndexFilePath = Path.Combine(WorkFolderPath, "ImmatureIndex.dat");
				ImmatureIndexFileManager = new DigestableSafeMutexIoManager(immatureIndexFilePath, digestRandomIndex: -1);

				StartingFilter = StartingFilters.GetStartingFilter(Network);
				StartingHeight = StartingFilter.Header.Height;

				ImmatureFilters = new List<FilterModel>(150);

				IndexLock = new AsyncLock();

				using (await IndexLock.LockAsync().ConfigureAwait(false))
				using (await MatureIndexFileManager.Mutex.LockAsync().ConfigureAwait(false))
				using (await ImmatureIndexFileManager.Mutex.LockAsync().ConfigureAwait(false))
				{
					IoHelpers.EnsureDirectoryExists(WorkFolderPath);

					await EnsureBackwardsCompatibilityAsync().ConfigureAwait(false);

					if (Network == Network.RegTest)
					{
						MatureIndexFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
						ImmatureIndexFileManager.DeleteMe();
					}

					if (!MatureIndexFileManager.Exists())
					{
						await MatureIndexFileManager.WriteAllLinesAsync(new[] { StartingFilter.ToLine() }).ConfigureAwait(false);
					}

					await InitializeFiltersAsync().ConfigureAwait(false);
				}
			}
		}

		private async Task DeleteIfDeprecatedAsync()
		{
			if (MatureIndexFileManager.Exists())
			{
				await DeleteIfDeprecatedAsync(MatureIndexFileManager).ConfigureAwait(false);
			}

			if (ImmatureIndexFileManager.Exists())
			{
				await DeleteIfDeprecatedAsync(ImmatureIndexFileManager).ConfigureAwait(false);
			}
		}

		private async Task DeleteIfDeprecatedAsync(DigestableSafeMutexIoManager ioManager)
		{
			string firstLine;
			using (var content = ioManager.OpenText())
			{
				firstLine = await content.ReadLineAsync().ConfigureAwait(false);
			}

			try
			{
				FilterModel.FromLine(firstLine);
			}
			catch
			{
				Logger.LogWarning("Old Index file detected. Deleting it.");
				MatureIndexFileManager.DeleteMe();
				ImmatureIndexFileManager.DeleteMe();
				Logger.LogWarning("Successfully deleted old Index file.");
			}
		}

		private async Task InitializeFiltersAsync()
		{
			try
			{
				if (MatureIndexFileManager.Exists())
				{
					using var sr = MatureIndexFileManager.OpenText();
					if (!sr.EndOfStream)
					{
						var lineTask = sr.ReadLineAsync();
						string line = null;
						while (lineTask != null)
						{
							if (line is null)
							{
								line = await lineTask.ConfigureAwait(false);
							}

							lineTask = sr.EndOfStream ? null : sr.ReadLineAsync();

							ProcessLine(line, enqueue: false);

							line = null;
						}
					}
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
				Logger.LogError("Mature index got corrupted. Deleting both mature and immature index...");
				MatureIndexFileManager.DeleteMe();
				ImmatureIndexFileManager.DeleteMe();
				throw;
			}

			try
			{
				if (ImmatureIndexFileManager.Exists())
				{
					foreach (var line in await ImmatureIndexFileManager.ReadAllLinesAsync().ConfigureAwait(false)) // We can load ImmatureIndexFileManager to the memory, no problem.
					{
						ProcessLine(line, enqueue: true);
					}
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
				Logger.LogError("Immature index got corrupted. Deleting it...");
				ImmatureIndexFileManager.DeleteMe();
				throw;
			}
		}

		private void ProcessLine(string line, bool enqueue)
		{
			var filter = FilterModel.FromLine(line);
			if (!TryProcessFilter(filter, enqueue))
			{
				throw new InvalidOperationException("Index file inconsistency detected.");
			}
		}

		private bool TryProcessFilter(FilterModel filter, bool enqueue)
		{
			try
			{
				SmartHeaderChain.AddOrReplace(filter.Header);
				if (enqueue)
				{
					ImmatureFilters.Add(filter);
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				return false;
			}
		}

		private async Task EnsureBackwardsCompatibilityAsync()
		{
			try
			{
				// Before Wasabi 1.1.5
				var oldIndexFilePath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), $"Index{Network}.dat");

				// Before Wasabi 1.1.6
				var oldFileNames = new[]
				{
					"ImmatureIndex.dat" ,
					"ImmatureIndex.dat.dig",
					"MatureIndex.dat",
					"MatureIndex.dat.dig"
				};

				var oldIndexFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "BitcoinStore", Network.ToString());

				foreach (var fileName in oldFileNames)
				{
					var oldFilePath = Path.Combine(oldIndexFolderPath, fileName);
					if (File.Exists(oldFilePath))
					{
						string newFilePath = oldFilePath.Replace(oldIndexFolderPath, WorkFolderPath);
						if (File.Exists(newFilePath))
						{
							File.Delete(newFilePath);
						}

						File.Move(oldFilePath, newFilePath);
					}
				}

				if (File.Exists(oldIndexFilePath))
				{
					string[] allLines = await File.ReadAllLinesAsync(oldIndexFilePath).ConfigureAwait(false);
					var matureLines = allLines.SkipLast(100);
					var immatureLines = allLines.TakeLast(100);

					await MatureIndexFileManager.WriteAllLinesAsync(matureLines).ConfigureAwait(false);
					await ImmatureIndexFileManager.WriteAllLinesAsync(immatureLines).ConfigureAwait(false);

					File.Delete(oldIndexFilePath);
				}

				await DeleteIfDeprecatedAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Backwards compatibility could not be ensured. Exception: {ex}.");
			}
		}

		public async Task AddNewFiltersAsync(IEnumerable<FilterModel> filters, CancellationToken cancel)
		{
			var successAny = false;
			foreach (var filter in filters)
			{
				var success = false;
				using (await IndexLock.LockAsync().ConfigureAwait(false))
				{
					success = TryProcessFilter(filter, enqueue: true);
				}
				successAny = successAny || success;

				if (success)
				{
					NewFilter?.Invoke(this, filter); // Event always outside the lock.
				}
			}

			if (successAny)
			{
				_ = TryCommitToFileAsync(TimeSpan.FromSeconds(3), cancel);
			}
		}

		public async Task<FilterModel> RemoveLastFilterAsync(CancellationToken cancel)
		{
			FilterModel filter = null;

			using (await IndexLock.LockAsync().ConfigureAwait(false))
			{
				filter = ImmatureFilters.Last();
				ImmatureFilters.RemoveLast();
				if (SmartHeaderChain.TipHeight != filter.Header.Height)
				{
					throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(ImmatureFilters)} are not in sync.");
				}
				SmartHeaderChain.RemoveLast();
			}

			Reorged?.Invoke(this, filter);

			_ = TryCommitToFileAsync(TimeSpan.FromSeconds(3), cancel);

			return filter;
		}

		public async Task<IEnumerable<FilterModel>> RemoveAllImmmatureFiltersAsync(CancellationToken cancel, bool deleteAndCrashIfMature = false)
		{
			var removed = new List<FilterModel>();
			using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
			{
				if (ImmatureFilters.Any())
				{
					Logger.LogWarning($"Filters got corrupted. Reorging {ImmatureFilters.Count} immature filters in an attempt to fix them.");
				}
				else
				{
					Logger.LogCritical($"Filters got corrupted and have no more immature filters.");

					if (deleteAndCrashIfMature)
					{
						Logger.LogCritical($"Deleting all filters and crashing the software...");

						using (await MatureIndexFileManager.Mutex.LockAsync(cancel).ConfigureAwait(false))
						using (await ImmatureIndexFileManager.Mutex.LockAsync(cancel).ConfigureAwait(false))
						{
							ImmatureIndexFileManager.DeleteMe();
							MatureIndexFileManager.DeleteMe();
						}

						Environment.Exit(2);
					}
				}
			}

			while (ImmatureFilters.Any())
			{
				removed.Add(await RemoveLastFilterAsync(cancel).ConfigureAwait(false));
			}

			return removed;
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
						await Task.Delay(throttle, cancel).ConfigureAwait(false);
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
					Interlocked.Exchange(ref _throttleId, 0); // So to notify the currently throttled threads that they do not have to run.
				}

				using (await MatureIndexFileManager.Mutex.LockAsync(cancel).ConfigureAwait(false))
				using (await ImmatureIndexFileManager.Mutex.LockAsync(cancel).ConfigureAwait(false))
				using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
				{
					// Do not feed the cancellationToken here I always want this to finish running for safety.
					var currentImmatureLines = ImmatureFilters.Select(x => x.ToLine()).ToArray(); // So we do not read on ImmatureFilters while removing them.
					var matureLinesToAppend = currentImmatureLines.SkipLast(100);
					var immatureLines = currentImmatureLines.TakeLast(100);
					var tasks = new Task[] { MatureIndexFileManager.AppendAllLinesAsync(matureLinesToAppend), ImmatureIndexFileManager.WriteAllLinesAsync(immatureLines) };
					while (ImmatureFilters.Count > 100)
					{
						ImmatureFilters.RemoveFirst();
					}
					await Task.WhenAll(tasks).ConfigureAwait(false);
				}
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		public async Task ForeachFiltersAsync(Func<FilterModel, uint, Task> todo, Height fromHeight)
		{
			using (await MatureIndexFileManager.Mutex.LockAsync().ConfigureAwait(false))
			using (await IndexLock.LockAsync().ConfigureAwait(false))
			{
				var firstImmatureHeight = ImmatureFilters.FirstOrDefault()?.Header?.Height;
				if (!firstImmatureHeight.HasValue || firstImmatureHeight.Value > fromHeight)
				{
					if (MatureIndexFileManager.Exists())
					{
						uint height = StartingHeight;
						using var sr = MatureIndexFileManager.OpenText();
						if (!sr.EndOfStream)
						{
							var lineTask = sr.ReadLineAsync();
							Task tTask = Task.CompletedTask;
							string line = null;
							while (lineTask != null)
							{
								if (firstImmatureHeight == height)
								{
									break; // Let's use our the immature filters from here on. The content is the same, just someone else modified the file.
								}

								if (line is null)
								{
									line = await lineTask.ConfigureAwait(false);
								}

								lineTask = sr.EndOfStream ? null : sr.ReadLineAsync();

								if (height < fromHeight.Value)
								{
									height++;
									line = null;
									continue;
								}

								var filter = FilterModel.FromLine(line);

								await tTask.ConfigureAwait(false);
								tTask = todo(filter, GetBestKnownHeightNoLock(filter));

								height++;

								line = null;
							}
							await tTask.ConfigureAwait(false);
						}

						while (!sr.EndOfStream)
						{
							var line = await sr.ReadLineAsync().ConfigureAwait(false);

							if (firstImmatureHeight == height)
							{
								break; // Let's use our the immature filters from here on. The content is the same, just someone else modified the file.
							}

							if (height < fromHeight.Value)
							{
								height++;
								continue;
							}

							var filter = FilterModel.FromLine(line);

							await todo(filter, GetBestKnownHeightNoLock(filter)).ConfigureAwait(false);
							height++;
						}
					}
				}

				foreach (FilterModel filter in ImmatureFilters)
				{
					await todo(filter, GetBestKnownHeightNoLock(filter)).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		/// Gets the best known height, assuming this filter is known.
		/// </summary>
		private uint GetBestKnownHeightNoLock(FilterModel filter)
		{
			var bestKnownHeight = ImmatureFilters.LastOrDefault()?.Header?.Height;
			if (bestKnownHeight is null)
			{
				bestKnownHeight = filter.Header.Height;
			}

			return bestKnownHeight.Value;
		}
	}
}
