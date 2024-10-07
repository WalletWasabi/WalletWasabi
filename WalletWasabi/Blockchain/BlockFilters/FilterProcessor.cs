using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.BlockFilters;

public class FilterProcessor
{
	public FilterProcessor(BitcoinStore bitcoinStore)
	{
		_bitcoinStore = bitcoinStore;
	}

	private readonly BitcoinStore _bitcoinStore;

	public async Task ProcessAsync(uint serverBestHeight, FiltersResponseState filtersResponseState, IEnumerable<FilterModel> filters)
	{
		try
		{
			var hashChain = _bitcoinStore.SmartHeaderChain;
			hashChain.SetServerTipHeight(serverBestHeight);

			if (filtersResponseState == FiltersResponseState.NewFilters)
			{
				var firstFilter = filters.First();
				if (hashChain.TipHeight + 1 != firstFilter.Header.Height)
				{
					// We have a problem.
					// We have wrong filters, the heights are not in sync with the server's.
					Logger.LogError($"Inconsistent index state detected.{Environment.NewLine}" +
						$"{nameof(hashChain)}.{nameof(hashChain.TipHeight)}:{hashChain.TipHeight}{Environment.NewLine}" +
						$"{nameof(hashChain)}.{nameof(hashChain.HashesLeft)}:{hashChain.HashesLeft}{Environment.NewLine}" +
						$"{nameof(hashChain)}.{nameof(hashChain.TipHash)}:{hashChain.TipHash}{Environment.NewLine}" +
						$"{nameof(hashChain)}.{nameof(hashChain.HashCount)}:{hashChain.HashCount}{Environment.NewLine}" +
						$"{nameof(hashChain)}.{nameof(hashChain.ServerTipHeight)}:{hashChain.ServerTipHeight}{Environment.NewLine}" +
						$"{nameof(firstFilter)}.{nameof(firstFilter.Header)}.{nameof(firstFilter.Header.BlockHash)}:{firstFilter.Header.BlockHash}{Environment.NewLine}" +
						$"{nameof(firstFilter)}.{nameof(firstFilter.Header)}.{nameof(firstFilter.Header.Height)}:{firstFilter.Header.Height}");

					await _bitcoinStore.IndexStore.RemoveAllNewerThanAsync(hashChain.TipHeight).ConfigureAwait(false);
				}
				else
				{
					await _bitcoinStore.IndexStore.AddNewFiltersAsync(filters).ConfigureAwait(false);

					if (filters.Count() == 1)
					{
						Logger.LogInfo($"Downloaded filter for block {firstFilter.Header.Height}.");
					}
					else
					{
						Logger.LogInfo($"Downloaded filters for blocks from {firstFilter.Header.Height} to {filters.Last().Header.Height}.");
					}
				}
			}
			else if (filtersResponseState == FiltersResponseState.BestKnownHashNotFound)
			{
				// Reorg happened
				// 1. Rollback index
				FilterModel reorgedFilter = await _bitcoinStore.IndexStore.TryRemoveLastFilterAsync().ConfigureAwait(false)
					?? throw new InvalidOperationException("Fatal error: Failed to remove the reorged filter.");

				Logger.LogInfo($"REORG Invalid Block: {reorgedFilter.Header.BlockHash}.");
			}
			else if (filtersResponseState == FiltersResponseState.NoNewFilter)
			{
				// We are synced.
				// Assert index state.
				if (serverBestHeight > hashChain.TipHeight) // If the server's tip height is larger than ours, we're missing a filter, our index got corrupted.
				{
					// If still bad delete filters and crash the software?
					await _bitcoinStore.IndexStore.RemoveAllNewerThanAsync(hashChain.TipHeight).ConfigureAwait(false);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}
}
