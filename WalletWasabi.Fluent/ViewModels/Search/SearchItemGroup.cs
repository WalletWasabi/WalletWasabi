using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemGroup
	{
		public SearchItemGroup(SearchCategory category, IEnumerable<SearchItemViewModel> searchItems)
		{
			Category = category;
			SearchItems = searchItems;
		}

		public SearchCategory Category { get; }

		public IEnumerable<SearchItemViewModel> SearchItems { get; }
	}
}