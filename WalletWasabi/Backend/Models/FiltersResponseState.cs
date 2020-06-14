using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Backend.Models
{
	public enum FiltersResponseState
	{
		BestKnownHashNotFound, // When this happens, it's a reorg.
		NoNewFilter,
		NewFilters
	}
}
