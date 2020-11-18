using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public sealed record SearchResult(SearchCategory Category, IEnumerable<SearchItemViewModel> Items);
}