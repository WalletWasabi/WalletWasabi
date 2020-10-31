using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using System.Reactive;
using System.IO;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HomePageViewModel : NavBarItemViewModel
	{
		private readonly ReadOnlyObservableCollection<NavBarItemViewModel> NavBarItems;

		public HomePageViewModel(IScreen screen, WalletManagerViewModel walletManager, AddWalletPageViewModel addWalletPage) : base(screen)
		{
			Title = "Home";

			var list = new SourceList<NavBarItemViewModel>();
			list.Add(addWalletPage);

			walletManager.Items.ToObservableChangeSet()
				.Cast(x => x as NavBarItemViewModel)
				.Sort(SortExpressionComparer<NavBarItemViewModel>.Ascending(i => i.Title))
				.Merge(list.Connect())
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out NavBarItems)
				.AsObservableList();

			OpenWalletsFolderCommand = ReactiveCommand.Create(() => IoHelpers.OpenFolderInFileExplorer(walletManager.Model.WalletDirectories.WalletsDir));			
		}

		public override string IconName => "home_regular";

		public ReadOnlyObservableCollection<NavBarItemViewModel> Items => NavBarItems;

		public ReactiveCommand<Unit, Unit> OpenWalletsFolderCommand { get; }
	}
}
