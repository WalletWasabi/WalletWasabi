using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using System.Reactive;
using System.IO;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		private readonly ReadOnlyObservableCollection<NavBarItemViewModel> _items;

		public HomePageViewModel(NavigationStateViewModel navigationState, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage)
			: base(navigationState, NavigationTarget.HomeScreen, NavBarItemSelectionMode.Selected)
		{
			Title = "Home";

			var list = new SourceList<NavBarItemViewModel>();
			list.Add(addWalletPage);

			walletManager.Items.ToObservableChangeSet()
				.Cast(x => x as NavBarItemViewModel)
				.Sort(SortExpressionComparer<NavBarItemViewModel>.Ascending(i => i.Title))
				.Merge(list.Connect())
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _items)
				.AsObservableList();

			OpenWalletsFolderCommand = ReactiveCommand.Create(() => IoHelpers.OpenFolderInFileExplorer(walletManager.Model.WalletDirectories.WalletsDir));
		}

		public override string IconName => "home_regular";

		public ReadOnlyObservableCollection<NavBarItemViewModel> Items => _items;

		public ReactiveCommand<Unit, Unit> OpenWalletsFolderCommand { get; }
	}
}
