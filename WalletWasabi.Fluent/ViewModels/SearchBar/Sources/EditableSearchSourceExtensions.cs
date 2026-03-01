using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public static class EditableSearchSourceExtensions
{
	public static void Toggle(this EditableSearchSource searchSource, ISearchItem searchItem, bool isDisplayed)
	{
		if (isDisplayed)
		{
			searchSource.Add(searchItem);
		}
		else
		{
			searchSource.Remove(searchItem);
		}
	}

	public static void Toggle(this EditableSearchSource searchSource, IEnumerable<ISearchItem> searchItems, bool isDisplayed)
	{
		if (isDisplayed)
		{
			searchSource.Add(searchItems.ToArray());
		}
		else
		{
			searchSource.Remove(searchItems.ToArray());
		}
	}
}
