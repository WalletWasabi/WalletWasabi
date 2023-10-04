using System.Collections.Generic;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class NonActionableSearchItemNode
{
	public static NonActionableSearchItemNode<TObject, TProperty> Create<TObject, TProperty>(IEditableSearchSource searchSource, Setting<TObject, TProperty> setting, string name, string category, IEnumerable<string> keywords, string? icon, int priority, params NestedItemConfiguration<TProperty>[] nestedItemConfiguration)
	{
		return new NonActionableSearchItemNode<TObject, TProperty>(searchSource, setting, name, category, keywords, icon, nestedItemConfiguration) { Priority = priority };
	}
}

public class NonActionableSearchItemNode<TObject, TProperty> : ReactiveObject, INonActionableSearchItem
{
	private readonly IEditableSearchSource _editableSearchSource;
	private Setting<TObject, TProperty> _setting;
	private readonly NestedItemConfiguration<TProperty>[] _nestedItems;

	public NonActionableSearchItemNode(IEditableSearchSource editableSearchSource, Setting<TObject, TProperty> setting, string name, string category, IEnumerable<string> keywords, string? icon, params NestedItemConfiguration<TProperty>[] nestedItems)
	{
		_editableSearchSource = editableSearchSource;
		_setting = setting;
		_nestedItems = nestedItems;
		Name = name;
		Content = setting;
		Category = category;
		Keywords = keywords;
		Icon = icon;
		this.WhenAnyValue(item => item._setting.Value)
			.Do(HandleNested)
			.Subscribe();
	}

	private void HandleNested(TProperty property)
	{
		foreach (var nestedItem in _nestedItems)
		{
			var isVisible = nestedItem.IsVisible(property);
			if (isVisible)
			{
				_editableSearchSource.Add(nestedItem.Item);
			}
			else
			{
				_editableSearchSource.Remove(nestedItem.Item);
			}
		}
	}

	public object Content { get; }
	public string Name { get; }
	public ComposedKey Key => new(Name);
	public string Description => "";
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; set; }
	public int Priority { get; set; }
}
