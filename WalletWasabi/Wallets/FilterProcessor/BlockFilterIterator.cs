using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Wallets.FilterProcessor;

public class BlockFilterIterator(IIndexStore indexStore, int maxNumberFiltersInMemory = 1000)
{
    private readonly IIndexStore _indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
    private readonly Dictionary<uint, FilterModel> _cache = new();
	private readonly int _maxNumberFiltersInMemory = maxNumberFiltersInMemory > 0
		? maxNumberFiltersInMemory
		: throw new ArgumentOutOfRangeException(nameof(maxNumberFiltersInMemory), "Must be greater than zero");

	/// <summary>
	/// Gets block filter for the block of specified height.
	/// </summary>
	/// <remarks>Filter is immediately removed from the cache once the method returns. Repeated calls for single height are thus expensive.</remarks>
	public async Task<FilterModel> GetAndRemoveAsync(uint height, CancellationToken cancellationToken)
	{
		if (_cache.Remove(height, out var result))
		{
			return result;
		}

		// We don't have the next filter to process, so fetch another batch of filters from the database.
		_cache.Clear();

		var filtersBatch = await _indexStore.FetchBatchAsync(height, _maxNumberFiltersInMemory, cancellationToken).ConfigureAwait(false);

		// Check that we get a block filter and that the filter is actually the one we want as the previous command does not guarantee that we get such block.
		if (filtersBatch.Length == 0)
		{
			throw new UnreachableException($"No block was found for a batch starting with block height {height}.");
		}

		if (filtersBatch[0].Header.Height != height)
		{
			throw new UnreachableException($"Block filter for height {height} was not found.");
		}

		// _cache filters.
		uint expectedHeight = height + 1;

		// Do not store the first filter, the semantics is that the returned filter is no longer stored in the cache.
		foreach (var filter in filtersBatch.Skip(1))
		{
			// Make sure that the sequence of blocks is consecutive.
			if (expectedHeight != filter.Header.Height)
			{
				throw new UnreachableException($"Expected block with height {expectedHeight}, got {filter.Header.Height} (block hash: {filter.Header.BlockHash}).");
			}

			_cache[filter.Header.Height] = filter;
			expectedHeight++;
		}

		result = filtersBatch[0];

		return result;
	}

	public void RemoveNewerThan(uint height)
	{
		var keysToRemove = _cache.Keys
			.Where(key => key > height)
			.ToList();

		foreach (var heightToRemove in keysToRemove)
		{
			if (!_cache.Remove(heightToRemove))
			{
				throw new UnreachableException($"Filter {heightToRemove} was already removed from the _cache.");
			}
		}
	}
}
