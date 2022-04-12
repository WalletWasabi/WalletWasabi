using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItem;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public interface ISearchItemSource
{
	IObservable<IChangeSet<ISearchItem, ComposedKey>> Source { get; }
}