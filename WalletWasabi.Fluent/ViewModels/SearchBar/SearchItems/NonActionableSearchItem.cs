using System.Collections.Generic;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class NonActionableSearchItem : ISearchItem
{
	public NonActionableSearchItem(UiContext uiContext, object content, string name, string category, IEnumerable<string> keywords, string? icon, IObservable<bool>? isVisible = default)
	{
		Name = name;
		Content = content;
		Category = category;
		Keywords = keywords;
		Icon = icon;
		isVisible?
			.Do(
				visible =>
				{
					if (visible)
					{
						uiContext.EditableSearchSource.Add(this);
					}
					else
					{
						uiContext.EditableSearchSource.Remove(this);
					}
				})
			.Subscribe();
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
