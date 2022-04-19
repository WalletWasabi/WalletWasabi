using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public interface ISearchItem
{
	string Name { get; }
	string Description { get; }
	ComposedKey Key { get; }
	string? Icon { get; set; }
	string Category { get; }
	IEnumerable<string> Keywords { get; }
	bool IsDefault { get; }
}