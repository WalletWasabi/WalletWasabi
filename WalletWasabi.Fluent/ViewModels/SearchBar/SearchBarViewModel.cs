using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar
{
    public class SearchBarViewModel : ReactiveObject
    {
        private readonly ReadOnlyObservableCollection<SearchItem> items;
        private string searchText;

        public SearchBarViewModel(IObservable<SearchItem> itemsObservable)
        {
            var source = new SourceCache<SearchItem, ComposedKey>(item => item.Key);
            source.PopulateFrom(itemsObservable);

            var filterPredicate = this
                .WhenAnyValue(x => x.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .DistinctUntilChanged()
                .Select(SearchItemFilterFunc);

            source.Connect()
                .RefCount()
                .Filter(filterPredicate)
                .Bind(out items)
                .DisposeMany()
                .Subscribe();
        }

        public ReadOnlyObservableCollection<SearchItem> Items => items;

        public string SearchText
        {
            get => searchText;
            set => this.RaiseAndSetIfChanged(ref searchText, value);
        }

        Func<SearchItem, bool> SearchItemFilterFunc(string text) => searchItem => string.IsNullOrEmpty(text) || searchItem.Name.ToLower().Contains(text.ToLower());
    }
}