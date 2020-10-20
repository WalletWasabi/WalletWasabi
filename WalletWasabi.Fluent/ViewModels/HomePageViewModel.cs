using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		private readonly ReadOnlyObservableCollection<NavBarItemViewModel> _items;

		public HomePageViewModel(IScreen screen, WalletManagerViewModel walletManager) : base(screen)
		{
			Title = "Home";

			var list = new SourceList<NavBarItemViewModel>();
			list.Add(new AddWalletPageViewModel(screen));

			walletManager.Items.ToObservableChangeSet()
				.Cast(x => x as NavBarItemViewModel)
				.Sort(SortExpressionComparer<NavBarItemViewModel>.Ascending(i=>i.Title))
				.Or(list.Connect())
				.Sort(SortExpressionComparer<NavBarItemViewModel>.Ascending(i => i is AddWalletPageViewModel ? 1 : 0))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _items)
				.AsObservableList();
		}

		public override string IconName => "home_regular";

		public ReadOnlyObservableCollection<NavBarItemViewModel> Items => _items;
	}
}
