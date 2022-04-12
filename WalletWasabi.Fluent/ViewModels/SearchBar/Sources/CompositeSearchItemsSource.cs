using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItem;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class CompositeSearchItemsSource : ISearchItemSource
{
	private readonly ISearchItemSource[] _sources;

	public CompositeSearchItemsSource(params ISearchItemSource[] sources)
	{
		_sources = sources;
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Source => _sources.Select(r => r.Source).Merge();
}