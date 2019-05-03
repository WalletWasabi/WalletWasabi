using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
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

		private object IndexLock { get; set; }
		private List<FilterModel> Index { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			IndexFilePath = Path.Combine(WorkFolderPath, IndexFileName);

			StartingFilter = StartingFilters.GetStartingFilter(Network);
			StartingHeight = StartingFilters.GetStartingHeight(Network);

			Index = new List<FilterModel>();

			IoHelpers.EnsureDirectoryExists(WorkFolderPath);

			if (Network == Network.RegTest)
			{
				IoHelpers.BetterDelete(IndexFilePath); // RegTest is not a global ledger, better to delete it.
			}

			if (!File.Exists(IndexFilePath))
			{
				Index.Add(StartingFilter);
				await File.WriteAllLinesAsync(IndexFilePath, Index.Select(x => x.ToHeightlessLine()));
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
	}
}
