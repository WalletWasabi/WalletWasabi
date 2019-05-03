using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
		private string IndexFilePath { get; set; }

		private FilterModel StartingFilter { get; set; }
		private Height StartingHeight { get; set; }
		private List<FilterModel> Index { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			IoHelpers.EnsureDirectoryExists(WorkFolderPath);

			Network = Guard.NotNull(nameof(network), network);
			IndexFilePath = Path.Combine(WorkFolderPath, IndexFileName);

			StartingFilter = StartingFilters.GetStartingFilter(Network);
			StartingHeight = StartingFilters.GetStartingHeight(Network);

			Index = new List<FilterModel>();

			TryEnsureBackwardsCompatibility();

			if (Network == Network.RegTest)
			{
				IoHelpers.BetterDelete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
			}

			if (!File.Exists(IndexFilePath))
			{
				await File.WriteAllLinesAsync(IndexFilePath, new[] { StartingFilter.ToHeightlessLine() });
			}

			var height = StartingHeight;
			try
			{
				foreach (var line in await File.ReadAllLinesAsync(IndexFilePath))
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
				await File.WriteAllLinesAsync(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
			}
		}

		private void TryEnsureBackwardsCompatibility()
		{
			try
			{
				// Before Wasabi 1.1.5
				var oldIndexFilepath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), $"Index{Network}.dat");
				if (File.Exists(oldIndexFilepath))
				{
					File.Move(oldIndexFilepath, IndexFilePath);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IndexStore>($"Backwards compatibility couldn't be ensured. Exception: {ex.ToString()}");
			}
		}

		public async Task AddNewFilterAsync(FilterModel filter)
		{
			Index.Add(filter);
			await File.AppendAllLinesAsync(IndexFilePath, new[] { filter.ToHeightlessLine() });
		}

		public async Task RemoveLastFilterAsync()
		{
			Index.RemoveLast();
			await File.WriteAllLinesAsync(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
		}

		public FilterModel GetLastFilter()
		{
			return Index.Last();
		}

		public Height? TryGetHeight(uint256 blockHash)
		{
			return Index.FirstOrDefault(x => x.BlockHash == blockHash)?.BlockHeight;
		}

		public IEnumerable<FilterModel> GetFilters()
		{
			return Index.ToList();
		}

		public int CountFilters() => Index.Count;
	}
}
