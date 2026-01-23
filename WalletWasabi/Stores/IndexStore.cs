using Microsoft.Data.Sqlite;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Stores;

/// <summary>
/// Manages to store the filters safely.
/// </summary>
public class IndexStore : IIndexStore, IAsyncDisposable
{
	public IndexStore(string workFolderPath, Network network, SmartHeaderChain smartHeaderChain)
	{
		_smartHeaderChain = smartHeaderChain;
		_network = network;

		workFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(workFolderPath);

		_newIndexFilePath = Path.Combine(workFolderPath, "IndexStore.sqlite");

		if (network == Network.RegTest)
		{
			DeleteIndex(_newIndexFilePath);
		}

		IndexStorage = CreateBlockFilterSqliteStorage();
	}

	private BlockFilterSqliteStorage CreateBlockFilterSqliteStorage()
	{
		try
		{
			return BlockFilterSqliteStorage.FromFile(dataSource: _newIndexFilePath, startingFilter: StartingFilters.GetStartingFilter(_network));
		}
		catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 11) // 11 ~ SQLITE_CORRUPT error code
		{
			Logger.LogError($"Failed to open SQLite storage file because it's corrupted. Deleting the storage file '{_newIndexFilePath}'.");

			DeleteIndex(_newIndexFilePath);
			throw;
		}
	}

	public event EventHandler<FilterModel>? Reorged;

	public event EventHandler<FilterModel[]>? NewFilters;

	/// <summary>SQLite file path for migration purposes.</summary>
	private readonly string _newIndexFilePath;

	/// <summary>NBitcoin network.</summary>
	private readonly Network _network;

	private readonly SmartHeaderChain _smartHeaderChain;

	/// <summary>Task completion source that is completed once a <see cref="InitializeAsync(CancellationToken)"/> finishes.</summary>
	/// <remarks><c>true</c> if it finishes successfully, <c>false</c> in all other cases.</remarks>
	public TaskCompletionSource<bool> InitializedTcs { get; } = new();

	/// <summary>Filter disk storage.</summary>
	/// <remarks>Guarded by <see cref="_indexLock"/>.</remarks>
	private BlockFilterSqliteStorage IndexStorage { get; set; }

	/// <summary>Guards <see cref="IndexStorage"/>.</summary>
	private readonly AsyncLock _indexLock = new();

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		try
		{
			using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_network == Network.Main && IndexStorage.GetPragmaUserVersion() == 0)
				{
					SmartResyncIfCorrupted();
					IndexStorage.SetPragmaUserVersion(1);
				}

				await InitializeFiltersNoLockAsync(cancellationToken).ConfigureAwait(false);

				// Initialization succeeded.
				InitializedTcs.SetResult(true);
			}
		}
		catch (Exception)
		{
			InitializedTcs.SetResult(false);
			throw;
		}
	}

	/// <remarks>Guarded by <see cref="_indexLock"/>.</remarks>
	private Task InitializeFiltersNoLockAsync(CancellationToken cancellationToken)
	{
		try
		{
			int i = 0;

			// Read last N filters. There is no need to read all of them.
			foreach (FilterModel filter in IndexStorage.FetchLast(n: 5000))
			{
				i++;

				if (!TryProcessFilterNoLock(filter, enqueue: false))
				{
					throw new InvalidOperationException("Index file inconsistency detected.");
				}

				cancellationToken.ThrowIfCancellationRequested();
			}

			Logger.LogDebug($"Loaded {i} lines from the mature index file.");
		}
		catch (InvalidOperationException ex)
		{
			// We found a corrupted entry. Clear the corrupted database and stop here.
			Logger.LogError("Filter index got corrupted. Clearing the filter index...");
			Logger.LogDebug(ex);
			IndexStorage.Clear();
			throw;
		}

		return Task.CompletedTask;
	}

	/// <remarks>Requires <see cref="_indexLock"/> lock acquired.</remarks>
	private bool TryProcessFilterNoLock(FilterModel filter, bool enqueue)
	{
		try
		{
			if (!IsCorrect(_smartHeaderChain, filter))
			{
				throw new InvalidOperationException($"Header doesn't point to previous header.");
			}

			_smartHeaderChain.AppendTip(filter.Header);

			if (enqueue)
			{
				if (!IndexStorage.TryAppend(filter))
				{
					throw new InvalidOperationException("Failed to append filter to the database.");
				}
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
	}

	private bool IsCorrect(SmartHeaderChain c, FilterModel m)
	{
		// If this is the first filter that we receive then it is correct only if it is starting one.
		if (c.Tip is not { } tip)
		{
			return true;
		}
		if (m.Filter.IsBip158())
		{
			// We received a bip158-compatible filter, and it matches the tip's header, which means the previous filter
			// was also a bip158-compatible one, and it is the correct one.
			if (m.Filter.GetHeader(tip.HeaderOrPrevBlockHash) == m.Header.HeaderOrPrevBlockHash || c.HashCount == 1)
			{
				return true;
			}

			// In case the previous filter is Bip158-compatible it should have passed the previous condition so, the
			// received filter did match.
			var previousFilter = IndexStorage.Fetch(tip.Height, 1).First();
			if (previousFilter.Filter.IsBip158())
			{
				return false;
			}

			// If we received a bip158-compatible filter for first time we accept it.
			return true;
		}
		else // Non-standard Wasabi Filter
		{
			if (m.Header.HeaderOrPrevBlockHash == tip.BlockHash)
			{
				return true;
			}

			var previousFilter = IndexStorage.Fetch(tip.Height, 1).First();
			if (previousFilter.Filter.IsBip158())
			{
				throw new InvalidOperationException("The received filter is not Wasabi filter while the previous one is a standard bip158 and it is not possible to verify the chain.");
			}

			return false;
		}
	}

	public async Task AddNewFiltersAsync(IEnumerable<FilterModel> filters)
	{
		using (await _indexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			using SqliteTransaction sqliteTransaction = IndexStorage.BeginTransaction();

			int processed = 0;

			try
			{
				foreach (FilterModel filter in filters)
				{
					if (!TryProcessFilterNoLock(filter, enqueue: true))
					{
						throw new InvalidOperationException($"Failed to process filter with height {filter.Header.Height}.");
					}

					processed++;
				}
			}
			finally
			{
				sqliteTransaction.Commit();

				if (processed > 0)
				{
					NewFilters.SafeInvoke(this, filters.Take(processed).ToArray());
				}
			}
		}
	}

	public async Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken)
	{
		using (await _indexLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return IndexStorage.Fetch(fromHeight: fromHeight, limit: batchSize).ToArray();
		}
	}

	public Task<FilterModel?> TryRemoveLastFilterAsync()
	{
		return TryRemoveLastFilterIfNewerThanAsync(height: null);
	}

	private async Task<FilterModel?> TryRemoveLastFilterIfNewerThanAsync(uint? height)
	{
		FilterModel? filter;

		using (await _indexLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
		{
			if (height is null)
			{
				if (!IndexStorage.TryRemoveLast(out filter))
				{
					return null;
				}
			}
			else
			{
				if (!IndexStorage.TryRemoveLastIfNewerThan(height.Value, out filter))
				{
					return null;
				}
			}

			if (_smartHeaderChain.TipHeight != filter.Header.Height)
			{
				throw new InvalidOperationException($"{nameof(SmartHeaderChain)} and {nameof(IndexStorage)} are not in sync.");
			}

			_smartHeaderChain.RemoveTip();
		}

		Reorged?.Invoke(this, filter);

		return filter;
	}

	public async Task RemoveAllNewerThanAsync(uint height)
	{
		while (true)
		{
			FilterModel? filterModel = await TryRemoveLastFilterIfNewerThanAsync(height).ConfigureAwait(false);

			if (filterModel is null)
			{
				break;
			}
		}
	}

	private void SmartResyncIfCorrupted()
	{
		uint deleteAllUnderHeight = 550000;
		uint batchSize = 100;
		byte referenceByte = 253;
		uint lastHeightPotentiallyAffected = 861657;
		var falsePositives = new Dictionary<uint, byte[]>()
		{
			#region falsePositives
			{ 511524, [4, 1, 14, 183, 128] }, { 519416, [80, 1, 57, 244, 45] }, { 531557, [131, 15, 13, 223, 115] },
			{ 536570, [89, 1, 224, 189, 2] }, { 542389, [2, 5, 57, 75, 176] }, { 543619, [165, 1, 253, 145, 239] },
			{ 544397, [17, 1, 76, 79, 129] }, { 545248, [204, 1, 242, 94, 144] }, { 550689, [80, 1, 121, 245, 132] },
			{ 551083, [71, 1, 131, 68, 242] }, { 555801, [12, 1, 136, 117, 8] }, { 556285, [100, 2, 185, 171, 205] },
			{ 556525, [53, 2, 43, 171, 149] }, { 557143, [20, 2, 94, 111, 108] }, { 558873, [47, 1, 140, 34, 174] },
			{ 560124, [129, 6, 105, 67, 77] }, { 561454, [69, 2, 11, 51, 186] }, { 563722, [1, 2, 208, 81, 245] },
			{ 566467, [147, 1, 89, 143, 38] }, { 572957, [92, 4, 1, 16, 7] }, { 574982, [48, 2, 196, 79, 132] },
			{ 578132, [138, 1, 67, 147, 41] }, { 581823, [166, 2, 97, 47, 214] }, { 582569, [153, 1, 17, 164, 28] },
			{ 584201, [141, 4, 54, 176, 3] }, { 586189, [116, 2, 168, 117, 17] }, { 587957, [133, 5, 72, 127, 75] },
			{ 596045, [171, 1, 8, 5, 125] }, { 598261, [103, 3, 25, 184, 12] }, { 600816, [144, 1, 29, 169, 72] },
			{ 605073, [155, 8, 46, 117, 64] }, { 606445, [66, 4, 178, 62, 86] }, { 609116, [159, 2, 39, 98, 194] },
			{ 617442, [131, 1, 42, 20, 4] }, { 624940, [122, 1, 241, 108, 246] }, { 628362, [160, 3, 50, 142, 30] },
			{ 630705, [71, 1, 180, 207, 144] }, { 631018, [176, 1, 14, 124, 137] }, { 633325, [114, 3, 33, 34, 112] },
			{ 634938, [235, 1, 88, 36, 34] }, { 635468, [165, 3, 90, 220, 65] }, { 636832, [204, 3, 33, 89, 48] },
			{ 638597, [77, 2, 117, 100, 224] }, { 640110, [44, 3, 183, 194, 160] }, { 643214, [104, 1, 100, 244, 170] },
			{ 643912, [166, 1, 102, 200, 131] }, { 644613, [153, 2, 104, 73, 65] }, { 645208, [58, 1, 10, 30, 237] },
			{ 646501, [204, 3, 15, 214, 17] }, { 652612, [59, 2, 132, 125, 103] }, { 653387, [220, 3, 222, 182, 148] },
			{ 654299, [124, 1, 147, 106, 0] }, { 655678, [47, 2, 22, 211, 0] }, { 656831, [125, 1, 27, 216, 67] },
			{ 658032, [232, 2, 197, 220, 100] }, { 666079, [74, 1, 142, 25, 148] }, { 666298, [184, 3, 6, 131, 175] },
			{ 668454, [201, 5, 92, 102, 88] }, { 669970, [73, 3, 159, 171, 140] }, { 674934, [133, 5, 120, 208, 97] },
			{ 675087, [229, 4, 82, 249, 84] }, { 678209, [31, 7, 9, 210, 104] }, { 678547, [140, 2, 45, 92, 100] },
			{ 680608, [61, 4, 15, 53, 144] }, { 682931, [4, 5, 132, 222, 228] }, { 685448, [81, 5, 207, 108, 40] },
			{ 692383, [8, 4, 60, 34, 147] }, { 695309, [57, 13, 184, 7, 148] }, { 698424, [20, 13, 69, 143, 53] },
			{ 701229, [221, 12, 9, 250, 115] }, { 702027, [182, 5, 31, 99, 113] }, { 702035, [147, 11, 54, 17, 129] },
			{ 704091, [79, 19, 19, 161, 67] }, { 707044, [204, 9, 177, 101, 124] }, { 708357, [227, 17, 9, 228, 144] },
			{ 708904, [237, 17, 188, 151, 132] }, { 712365, [23, 16, 5, 243, 207] }, { 715430, [57, 18, 211, 173, 79] },
			{ 718241, [133, 18, 39, 144, 213] }, { 719407, [201, 5, 5, 103, 182] }, { 721047, [25, 14, 0, 14, 129] },
			{ 724374, [227, 8, 175, 88, 213] }, { 724590, [133, 2, 112, 124, 154] },
			{ 726156, [50, 11, 246, 172, 236] }, { 726802, [146, 10, 28, 73, 194] }, { 732027, [235, 17, 124, 82, 5] },
			{ 732876, [219, 29, 77, 82, 157] }, { 736421, [22, 13, 132, 141, 210] },
			{ 738127, [121, 20, 123, 228, 249] }, { 740479, [178, 9, 168, 102, 3] }, { 743797, [57, 2, 32, 48, 0] },
			{ 745650, [136, 13, 44, 137, 221] }, { 751028, [180, 20, 143, 134, 193] },
			{ 751677, [65, 13, 188, 173, 14] }, { 753763, [155, 13, 104, 88, 194] }, { 760042, [14, 19, 66, 88, 135] },
			{ 760738, [196, 12, 19, 76, 15] }, { 761191, [208, 2, 52, 84, 40] }, { 764004, [144, 13, 109, 112, 21] },
			{ 771887, [195, 17, 156, 243, 211] }, { 773271, [44, 10, 140, 125, 211] },
			{ 774165, [107, 20, 46, 67, 145] }, { 774216, [105, 27, 1, 68, 27] }, { 774603, [252, 15, 82, 180, 141] },
			{ 777158, [73, 9, 79, 188, 128] }, { 778782, [60, 27, 65, 11, 76] }, { 778967, [255, 20, 57, 182, 12] },
			{ 780475, [219, 25, 50, 40, 201] }, { 781932, [132, 14, 205, 27, 68] }, { 782197, [168, 23, 39, 205, 218] },
			{ 786084, [239, 10, 53, 186, 11] }, { 788296, [155, 18, 116, 32, 181] }, { 790898, [55, 10, 101, 20, 230] },
			{ 791585, [121, 19, 157, 166, 93] }, { 792042, [8, 21, 165, 48, 91] }, { 794031, [236, 16, 148, 95, 89] },
			{ 796999, [17, 24, 148, 200, 58] }, { 797443, [127, 25, 73, 3, 88] }, { 799525, [162, 26, 2, 152, 118] },
			{ 806966, [204, 26, 13, 46, 30] }, { 807949, [159, 29, 221, 230, 50] }, { 809343, [60, 23, 104, 204, 30] },
			{ 809982, [254, 4, 33, 82, 80] }, { 810573, [247, 22, 30, 63, 165] }, { 812427, [101, 1, 158, 216, 27] },
			{ 814691, [93, 29, 193, 127, 181] }, { 815889, [243, 25, 88, 26, 187] }, { 816440, [22, 26, 17, 47, 217] },
			{ 818755, [140, 35, 23, 229, 42] }, { 820358, [27, 21, 95, 49, 81] }, { 825577, [82, 21, 82, 182, 124] },
			{ 829078, [176, 24, 167, 232, 238] }, { 833595, [22, 17, 81, 212, 20] }, { 834202, [118, 23, 10, 208, 72] },
			{ 838327, [63, 27, 14, 126, 166] }, { 839255, [133, 20, 162, 147, 219] }, { 841188, [214, 2, 34, 204, 44] },
			{ 846233, [95, 11, 155, 23, 200] }, { 848132, [10, 1, 4, 126, 139] }, { 848184, [206, 16, 76, 137, 226] },
			{ 849339, [252, 7, 58, 6, 122] }, { 849806, [57, 12, 31, 77, 130] }, { 849936, [181, 11, 178, 186, 110] },
			{ 852121, [61, 29, 23, 161, 129] }, { 854972, [106, 39, 132, 97, 32] }, { 856445, [93, 21, 63, 244, 201] },
			{ 859032, [130, 5, 94, 15, 80] }, { 859496, [85, 28, 105, 76, 130] }, { 859606, [57, 9, 137, 188, 162] },
			{ 860959, [223, 6, 46, 91, 210] }, { 861255, [143, 8, 75, 55, 124] }, { 497765, [145, 189, 205, 124, 42] },
			{ 498265, [70, 112, 210, 211, 95] }, { 504757, [84, 148, 40, 145, 123] },
			{ 504859, [5, 169, 16, 180, 192] }, { 505607, [46, 119, 96, 241, 203] },
			{ 505669, [170, 162, 176, 20, 188] }, { 518084, [90, 38, 224, 175, 227] },
			{ 522741, [240, 177, 159, 22, 181] }, { 522895, [37, 215, 137, 176, 242] },
			{ 525723, [54, 161, 194, 247, 145] }, { 532611, [42, 162, 88, 33, 47] },
			{ 537553, [61, 131, 140, 227, 28] }, { 538528, [38, 55, 197, 8, 188] }, { 540550, [0, 32, 139, 64, 52] },
			{ 540790, [21, 107, 24, 95, 121] }, { 541664, [0, 59, 36, 226, 144] }, { 542989, [23, 181, 222, 242, 255] },
			{ 543087, [6, 143, 120, 223, 25] }, { 547388, [240, 191, 223, 24, 234] },
			{ 563624, [42, 57, 201, 244, 20] }, { 573295, [49, 252, 224, 43, 30] }, { 577296, [5, 172, 101, 232, 68] },
			{ 580014, [50, 210, 138, 25, 237] }, { 580016, [54, 118, 24, 0, 141] }, { 582811, [199, 4, 200, 31, 65] },
			{ 586012, [202, 40, 97, 14, 212] }, { 592050, [143, 162, 192, 19, 178] },
			{ 594558, [127, 20, 190, 241, 148] }, { 596244, [80, 89, 123, 254, 39] },
			{ 620433, [22, 181, 102, 201, 212] }, { 682045, [168, 189, 246, 39, 24] }, { 740866, [133, 23, 5, 53, 77] }
			#endregion
		};

		var bestHeight = IndexStorage.GetBestHeight();

		if (bestHeight == StartingFilters.GetStartingFilter(Network.Main).Header.Height)
		{
			// Empty filters
			return;
		}

		if (bestHeight <= deleteAllUnderHeight)
		{
			// It is not worth it to try to estimate when there are that few filters, just delete them.
			// This will be really few users and those filters almost have no data anyway.
			Logger.LogWarning("Refreshing filters because they are potentially corrupted (wrong endian).");
			IndexStorage.RemoveNewerThan(StartingFilters.GetStartingFilter(Network.Main).Header.Height);
			return;
		}

		uint lastBatchToTest = (uint)Math.Min(bestHeight, lastHeightPotentiallyAffected) - batchSize + 1;
		uint currentHeight = StartingFilters.GetStartingFilter(Network.Main).Header.Height;

		while (true)
		{
			var batch = IndexStorage.Fetch(currentHeight, (int)batchSize).ToList();

			var foundInvalid = batch.Any(x => x.FilterData[^1] == referenceByte &&
											  !falsePositives.ContainsKey(x.Header.Height));

			if (!foundInvalid)
			{
				if (currentHeight == lastBatchToTest)
				{
					break;
				}
				currentHeight = Math.Min(lastBatchToTest, currentHeight + batchSize);
				continue;
			}

			var firstInvalidHeight = batch.Min(x => x.Header.Height);

			if (firstInvalidHeight <= deleteAllUnderHeight)
			{
				// A really old filter is invalid, better to delete everything
				Logger.LogWarning($"A really old filter is corrupted ({firstInvalidHeight}), better to delete the index.");
				IndexStorage.RemoveNewerThan(StartingFilters.GetStartingFilter(Network.Main).Header.Height);
				return;
			}
			Logger.LogWarning($"Filter ({firstInvalidHeight}) corrupted (wrong endian), deleting Index from {firstInvalidHeight - batchSize}.");

			// batchSize is an extra probabilistic security.
			IndexStorage.RemoveNewerThan(firstInvalidHeight - batchSize);
			break;
		}
	}

	private void DeleteIndex(string indexPath)
	{
		lock (_indexLock)
		{
			if (File.Exists(indexPath))
			{
				File.Delete(indexPath);
			}

			if (File.Exists($"{indexPath}-shm"))
			{
				File.Delete($"{indexPath}-shm");
			}

			if (File.Exists($"{indexPath}-wal"))
			{
				File.Delete($"{indexPath}-wal");
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		IndexStorage.Dispose();
		return ValueTask.CompletedTask;
	}
}
