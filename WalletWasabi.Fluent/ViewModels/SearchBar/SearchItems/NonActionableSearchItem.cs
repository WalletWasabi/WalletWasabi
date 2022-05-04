using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class NonActionableSearchItem : ISearchItem
{
	public NonActionableSearchItem(object content, string name, string category, IEnumerable<string> keywords, string? icon)
	{
		Name = name;
		Content = content;
		Category = category;
		Keywords = keywords;
		Icon = icon;
	}

	public string Name { get; }
	public object Content { get; }
	public ComposedKey Key => new(Name);
	public string Description => "";
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; set; }
}
