using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

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