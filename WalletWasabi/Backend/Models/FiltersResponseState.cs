namespace WalletWasabi.Backend.Models
{
    public enum FiltersResponseState
	{
		BestKnownHashNotFound, // When this happens, it's a reorg.
		NoNewFilter,
		NewFilters
	}
}
