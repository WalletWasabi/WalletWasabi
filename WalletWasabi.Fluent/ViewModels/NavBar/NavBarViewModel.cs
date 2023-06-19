using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

/// <summary>
/// The ViewModel that represents the structure of the sidebar.
/// </summary>
public partial class NavBarViewModel : ViewModelBase, IWalletNavigation
{
	[AutoNotify] private WalletPageViewModel? _selectedWallet;

	public NavBarViewModel(UiContext uiContext)
	{
		UiContext = uiContext;

		BottomItems = new ObservableCollection<NavBarItemViewModel>();

		UiContext.WalletRepository
				 .Wallets
				 .Transform(newWallet => new WalletPageViewModel(UiContext, newWallet))
				 .AutoRefresh(x => x.IsLoggedIn)
				 .Sort(SortExpressionComparer<WalletPageViewModel>.Descending(i => i.WalletModel.Auth.IsLoggedIn).ThenByAscending(x => x.WalletModel.Name))
				 .Bind(out var wallets)
				 .Subscribe();

		Wallets = wallets;
	}

	public void Activate()
	{
		this.WhenAnyValue(x => x.SelectedWallet)
			.Buffer(2, 1)
			.Select(buffer => (OldValue: buffer[0], NewValue: buffer[1]))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(x =>
			{
				if (x.OldValue is { } a)
				{
					a.IsSelected = false;
				}

				if (x.NewValue is { } b)
				{
					b.IsSelected = true;
					UiContext.WalletList.StoreLastSelectedWallet(b.WalletModel);
				}
			})
			.Subscribe();

		SelectedWallet = Wallets.FirstOrDefault(x => x.WalletModel.Name == UiContext.WalletRepository.DefaultWallet?.Name);
	}

	public ObservableCollection<NavBarItemViewModel> BottomItems { get; }

	public ReadOnlyObservableCollection<WalletPageViewModel> Wallets { get; }

	public async Task InitialiseAsync()
	{
		var bottomItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Bottom);

		foreach (var item in bottomItems)
		{
			var viewModel = await NavigationManager.MaterializeViewModelAsync(item);

			if (viewModel is INavBarItem navBarItem)
			{
				BottomItems.Add(new NavBarItemViewModel(navBarItem));
			}
		}
	}

	IWalletViewModel? IWalletNavigation.To(IWalletModel wallet)
	{
		SelectedWallet = Wallets.First(x => x.WalletModel.Name == wallet.Name);
		return SelectedWallet.WalletViewModel;
	}
}
