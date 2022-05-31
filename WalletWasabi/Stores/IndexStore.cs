using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Nito.AsyncEx;
namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class IndexStore : IAsyncDisposable
{
	private int _throttleId;

	public IndexStore(string workFolderPath, Network network, SmartHeaderChain smartHeaderChain)
	{
		WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(WorkFolderPath);
		var indexFilePath = Path.Combine(WorkFolderPath, "MatureIndex.dat");
		MatureIndexFileManager = new DigestableSafeIoManager(indexFilePath, useLastCharacterDigest: true);
		var immatureIndexFilePath = Path.Combine(WorkFolderPath, "ImmatureIndex.dat");
		ImmatureIndexFileManager = new DigestableSafeIoManager(immatureIndexFilePath, useLastCharacterDigest: true);

		Network = network;
		StartingFilter = StartingFilters.GetStartingFilter(Network);
		SmartHeaderChain = smartHeaderChain;
	}

	public event EventHandler<FilterModel>? Reorged;

	public event EventHandler<FilterModel>? NewFilter;

	private AbandonedTasks AbandonedTasks { get; } = new();

	private string WorkFolderPath { get; }
	private Network Network { get; }
	private DigestableSafeIoManager MatureIndexFileManager { get; }
	private DigestableSafeIoManager ImmatureIndexFileManager { get; }

	/// <summary>Lock for modifying <see cref="ImmatureFilters"/>. This should be lock #1.</summary>
	private AsyncLock IndexLock { get; } = new();

	/// <summary>Lock for accessing <see cref="MatureIndexFileManager"/>. This should be lock #2.</summary>
	private AsyncLock MatureIndexAsyncLock { get; } = new();

	/// <summary>Lock for accessing <see cref="ImmatureIndexFileManager"/>. This should be lock #3.</summary>
	private AsyncLock ImmatureIndexAsyncLock { get; } = new();

	public SmartHeaderChain SmartHeaderChain { get; }

	private FilterModel StartingFilter { get; }
	private uint StartingHeight => StartingFilter.Header.Height;
	private List<FilterModel> ImmatureFilters { get; } = new(150);

	public async Task InitializeAsync(CancellationToken cancel = default)
	{
		using (BenchmarkLogger.Measure())
		{
			using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
			using (await MatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
			using (await ImmatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				await EnsureBackwardsCompatibilityAsync().ConfigureAwait(false);

				if (Network == Network.RegTest)
				{
					MatureIndexFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
					ImmatureIndexFileManager.DeleteMe();
				}

				cancel.ThrowIfCancellationRequested();

				if (!MatureIndexFileManager.Exists())
				{
					await MatureIndexFileManager.WriteAllLinesAsync(new[] { StartingFilter.ToLine() }, CancellationToken.None).ConfigureAwait(false);
				}

				cancel.ThrowIfCancellationRequested();

				await InitializeFiltersAsync(cancel).ConfigureAwait(false);
			}
		}
	}

	private async Task DeleteIfDeprecatedAsync(DigestableSafeIoManager ioManager)
	{
		string? firstLine;
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

	private async Task InitializeFiltersAsync(CancellationToken cancel)
	{
		try
		{
			if (MatureIndexFileManager.Exists())
			{
				using (BenchmarkLogger.Measure(LogLevel.Debug, "MatureIndexFileManager loading"))
				{
					int i = 0;
					using StreamReader sr = MatureIndexFileManager.OpenText();
					if (!sr.EndOfStream)
					{
						while (true)
						{
							i++;
							cancel.ThrowIfCancellationRequested();
							string? line = await sr.ReadLineAsync().ConfigureAwait(false);

							if (line is null)
							{
								break;
							}

							ProcessLine(line, enqueue: false);
						}
					}

					Logger.LogDebug($"Loaded {i} lines from the mature index file.");
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// We found a corrupted entry. Stop here.
			// Delete the corrupted file.
			// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
			Logger.LogError("Mature index got corrupted. Deleting both mature and immature index...");
			MatureIndexFileManager.DeleteMe();
			ImmatureIndexFileManager.DeleteMe();
			throw;
		}

		cancel.ThrowIfCancellationRequested();

		try
		{
			if (ImmatureIndexFileManager.Exists())
			{
				foreach (var line in await ImmatureIndexFileManager.ReadAllLinesAsync(cancel).ConfigureAwait(false)) // We can load ImmatureIndexFileManager to the memory, no problem.
				{
					ProcessLine(line, enqueue: true);
					cancel.ThrowIfCancellationRequested();
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// We found a corrupted entry. Stop here.
			// Delete the corrupted file.
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
			if (IsWrongFilter(filter))
			{
				return false;
			}

			SmartHeaderChain.AppendTip(filter.Header);
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

	/// <summary>
	/// There was a bug when the server was sending wrong filters to the users for 24h. This function detects if the software tries to process the first wrong filter.
	/// </summary>
	private bool IsWrongFilter(FilterModel filter) =>
		Network == Network.Main && filter.Header.Height == 627278 && filter.ToLine() == "627278:00000000000000000002edea14cfbbcde1cdb6a14275d9ad36491aa5d8862747:fd4b018270de28c44316c049e4e4050d8a7de5746c7bb31d093831c56196e5f5464956c9ab7142b998a93cb80149b09353cb5d46dfeb44ecb0c8255fb0eb64247a405ab2305713e3418707be4fe87286b467ac86603749aeeac6c182022f0105b6c547b22b89608d0b57eaee2767150bff2354e4cdecef069d1a7f9356e5972ac7323c907b2e42775d032b4a12cc45e96eaa86d232e14808beca91f21c09003734bf77005d2dbfdfeaf19108e971f99b91046db0a021a128bb17b91c83766c65e924bb48af50c473f80e8e8569fd68aaba856b9e4f60efba08519d4ca0f1c0453e60f50a86398b11c860607f049e2bc5e1b6201470f5383601fcfbbea766f510768bac3145dc33443131e50d41bdfb92f5b3d9affc0bbaa85a4c40be2d9e582c3ca0c82251d191ec83dbd197cc1a9f070e6754d84c8ca1c0258d21264619cb523a9bda5556aded4f82e9a8955180c8c8772304bb5f2a5498d15f28b3f0d5d0b22aba14a18be7c8a100bb35b73385ce2b410126ac614f2260557444c3279b73dab148cd14e8800815a1248fa802901a4430817b59d936ceb3e1594d002c1b8d88ec8641f2b2d9827a42948c61c888fc32f070eeba19dda8f773c6cef04485575652f3e929507a9e24dcee53bdc548a317f1e019fbc7ac87c5314548cca6c0b85ebbca79bed2685ed7024a21349189d9a6c92b05aa53a7e241b6885575a19bd737040c263ac05b9920d2e31568afff3c545a827338e103096fbd8fb60ef317e55146b74260577064627bba812c7ca06c39b45d291d7bb9142c338012ccb97330873a0e256ca8aaff778348085e1c9e9942cd10a8444f0c708a798c1d701b4e1879d78ee51f3044ee0012e9929c6e5bfddba40ed04872065373af111ebe53a832f5563078ef274cd39a6b77c8155d8996b6a5617c2ff447dcf4a37a84bfbd1ab34b8a4012f0ccb82c8085668a52e722f8a59a63a07420d2fc67a4da39209fc0cdcd335b2b4670817218f92aee62c8d0e3e895d7aa0f3c69ba36687c9559cf38adfef8ef0ec90128d1efc3b69006ed2c026a1a904bdd1bc0aa1924c74e05b4fdd8316a4cc400d9ced30eaf0ed01f82a6ab59bdf1fbd7a7c6f7186e33411140b57673a0075946902c5890e5647df67183a84f5b2001be152a23741582b529116e2d3bd9964968b40080173e5339018edf609199f25021c757ff3b8d1add3731002784c4da7176cd8b201e3931c61272d17e4e58a2487666510889935d054f0b72817700:000000000000000000028dbd5c398fa064b00e07805af0a8806e6f3b6ffce2c0:1587639149";

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

			if (MatureIndexFileManager.Exists())
			{
				await DeleteIfDeprecatedAsync(MatureIndexFileManager).ConfigureAwait(false);
			}

			if (ImmatureIndexFileManager.Exists())
			{
				await DeleteIfDeprecatedAsync(ImmatureIndexFileManager).ConfigureAwait(false);
			}
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

			using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
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
			AbandonedTasks.AddAndClearCompleted(TryCommitToFileAsync(TimeSpan.FromSeconds(3), cancel));
		}
	}

	public async Task<FilterModel> RemoveLastFilterAsync(CancellationToken cancel)
	{
		FilterModel? filter = null;

		using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
		{
			filter = ImmatureFilters.Last();
			ImmatureFilters.RemoveLast();
			if (SmartHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(ImmatureFilters)} are not in sync.");
			}
			SmartHeaderChain.RemoveTip();
		}

		Reorged?.Invoke(this, filter);

		AbandonedTasks.AddAndClearCompleted(TryCommitToFileAsync(TimeSpan.FromSeconds(3), cancel));

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

					using (await MatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
					using (await ImmatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
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

			using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
			using (await MatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
			using (await ImmatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				// Do not feed the cancellationToken here I always want this to finish running for safety.
				var currentImmatureLines = ImmatureFilters.Select(x => x.ToLine()).ToArray(); // So we do not read on ImmatureFilters while removing them.
				var matureLinesToAppend = currentImmatureLines.SkipLast(100);
				var immatureLines = currentImmatureLines.TakeLast(100);

				// The order of the following lines is important.

				// 1) First delete the immature index. If we lose it because the mature index writing fails, we are OK with that.
				ImmatureIndexFileManager.DeleteMe();

				// 2) Attempt to update the mature index.
				await MatureIndexFileManager.AppendAllLinesAsync(matureLinesToAppend, CancellationToken.None).ConfigureAwait(false);

				// 3) Create new immature index.
				await ImmatureIndexFileManager.WriteAllLinesAsync(immatureLines, CancellationToken.None).ConfigureAwait(false);

				while (ImmatureFilters.Count > 100)
				{
					ImmatureFilters.RemoveFirst();
				}
			}
		}
		catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
		{
			Logger.LogTrace(ex);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public async Task ForeachFiltersAsync(Func<FilterModel, Task> todo, Height fromHeight, CancellationToken cancel = default)
	{
		using (await IndexLock.LockAsync(cancel).ConfigureAwait(false))
		using (await MatureIndexAsyncLock.LockAsync(cancel).ConfigureAwait(false))
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
						string? line = null;
						while (lineTask is { })
						{
							if (firstImmatureHeight == height)
							{
								break; // Let's use our the immature filters from here on. The content is the same, just someone else modified the file.
							}

							line ??= await lineTask.ConfigureAwait(false);

							lineTask = sr.EndOfStream ? null : sr.ReadLineAsync();

							if (height < fromHeight.Value)
							{
								height++;
								line = null;
								continue;
							}

							var filter = FilterModel.FromLine(line);

							await tTask.ConfigureAwait(false);
							tTask = todo(filter);

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

						await todo(filter).ConfigureAwait(false);
						height++;
					}
				}
			}

			foreach (FilterModel filter in ImmatureFilters.ToImmutableArray())
			{
				await todo(filter).ConfigureAwait(false);
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		await AbandonedTasks.WhenAllAsync().ConfigureAwait(false);
	}
}
