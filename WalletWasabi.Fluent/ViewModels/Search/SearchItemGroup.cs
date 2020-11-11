using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Search
{
	public class SearchItemGroup
	{
		public SearchItemGroup(string category, IEnumerable<SearchItemViewModel> searchItems)
		{
			Category = category;
			SearchItems = searchItems;
		}

		public string Category { get; }

		public IEnumerable<SearchItemViewModel> SearchItems { get; }
	}
}