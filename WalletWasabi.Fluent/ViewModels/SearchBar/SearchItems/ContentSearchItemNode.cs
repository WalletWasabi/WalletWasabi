using System.Collections.Generic;
using System.ComponentModel;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class ContentSearchItemNode
{
	public static SearchItemNode<TObject, TProperty> Create<TObject, TProperty>(EditableSearchSource searchSource, Setting<TObject, TProperty> setting, string name, string category, bool isDefault, IEnumerable<string> keywords, string? icon, int priority, bool isEnabled, params NestedItemConfiguration<TProperty>[] nestedItemConfiguration) where TObject : class, INotifyPropertyChanged
	{
		return new SearchItemNode<TObject, TProperty>(searchSource, setting, name, category, keywords, icon, isDefault, isEnabled, nestedItemConfiguration) { Priority = priority };
	}
}
