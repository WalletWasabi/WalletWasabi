using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public interface ISearchItemSource
{
	IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }
}