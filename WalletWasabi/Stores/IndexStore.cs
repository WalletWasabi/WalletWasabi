using NBitcoin;
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

		private FilterModel StartingFilter { get; set; }
		private Height StartingHeight { get; set; }
		private List<FilterModel> Index { get; set; }
		private SemaphoreSlim Semaphore { get; set; }

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

			Semaphore = new SemaphoreSlim(1);

			await IndexFileManager.WrapInMutexAsync(async () =>
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
					}
				}
				catch (FormatException)
				{
					// We found a corrupted entry. Stop here.
					// Fix the currupted file.
					await IndexFileManager.WriteAllLinesAsync(Index.Select(x => x.ToHeightlessLine()));
				}
			});
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

		public async Task AddNewFilterAsync(FilterModel filter)
		{
			try
			{
				await Semaphore.WaitAsync();

				Index.Add(filter);
				await IndexFileManager.WrapInMutexAsync(async () =>
				{
					await IndexFileManager.WriteAllLinesAsync(Index.Select(x => x.ToHeightlessLine()));
				});
			}
			finally
			{
				Semaphore.Release();
			}

			NewFilter?.Invoke(this, filter);
		}

		public async Task<FilterModel> RemoveLastFilterAsync()
		{
			FilterModel filter = null;
			try
			{
				await Semaphore.WaitAsync();

				filter = Index.Last();
				Index.RemoveLast();
				await IndexFileManager.WrapInMutexAsync(async () =>
				{
					await IndexFileManager.WriteAllLinesAsync(Index.Select(x => x.ToHeightlessLine()));
				});
			}
			finally
			{
				Semaphore.Release();
			}

			Reorged?.Invoke(this, filter);

			return filter;
		}

		public async Task<FilterModel> GetBestKnownFilterAsync()
		{
			FilterModel ret = null;
			try
			{
				await Semaphore.WaitAsync();

				ret = Index.Last();
			}
			finally
			{
				Semaphore.Release();
			}
			return ret;
		}

		public async Task<Height?> TryGetHeightAsync(uint256 blockHash)
		{
			Height? ret = null;
			try
			{
				await Semaphore.WaitAsync();

				ret = Index.FirstOrDefault(x => x.BlockHash == blockHash)?.BlockHeight;
			}
			finally
			{
				Semaphore.Release();
			}
			return ret;
		}

		public async Task<IEnumerable<FilterModel>> GetFiltersAsync()
		{
			List<FilterModel> ret = null;
			try
			{
				await Semaphore.WaitAsync();

				ret = Index.ToList();
			}
			finally
			{
				Semaphore.Release();
			}
			return ret;
		}

		public async Task<int> CountFiltersAsync()
		{
			int ret;
			try
			{
				await Semaphore.WaitAsync();

				ret = Index.Count;
			}
			finally
			{
				Semaphore.Release();
			}
			return ret;
		}
	}
}
