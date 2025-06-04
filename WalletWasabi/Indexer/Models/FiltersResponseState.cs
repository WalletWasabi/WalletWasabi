namespace WalletWasabi.Indexer.Models;

public enum FiltersResponseState
{
	BestKnownHashNotFound, // When this happens, it's a reorg.
	NoNewFilter,
	NewFilters
}
