using System.Collections.Generic;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public interface ISearchItem
{
	public string Name { get; }
	public string Description { get; }
	public ComposedKey Key { get; }
	public string? Icon { get; set; }
	public SearchCategory Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; }
	public int Priority { get; }
}
