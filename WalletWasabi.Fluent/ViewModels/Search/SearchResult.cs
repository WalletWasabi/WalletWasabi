using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Search;

public class SearchResult
{
	public SearchResult(SearchCategory category, IEnumerable<SearchItemViewModel> items)
	{
		Category = category;
		Items = items;
	}

	public SearchCategory Category { get; }

	public IEnumerable<SearchItemViewModel> Items { get; }
}
