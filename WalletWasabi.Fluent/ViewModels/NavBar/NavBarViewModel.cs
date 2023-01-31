using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

/// <summary>
/// The ViewModel that represents the structure of the sidebar.
/// </summary>
public partial class NavBarViewModel : ViewModelBase
{
	public NavBarViewModel()
	{
		TopItems = new ObservableCollection<NavBarItemViewModel>();
		BottomItems = new ObservableCollection<NavBarItemViewModel>();

		this.WhenAnyValue(x => x.SelectedWallet)
			.Where(x=> x is { })
			.Do(x =>  x!.OpenCommand.Execute(default))
			.Subscribe();

		SetDefaultSelection();
	}

	public ObservableCollection<NavBarItemViewModel> TopItems { get; }

	public ObservableCollection<NavBarItemViewModel> BottomItems { get; }

	public ObservableCollection<WalletViewModelBase> Wallets => UiServices.WalletManager.Wallets;


	[AutoNotify] private WalletViewModelBase? _selectedWallet;

	private void SetDefaultSelection()
	{
		var walletToSelect = Wallets.FirstOrDefault(item => item.WalletName == Services.UiConfig.LastSelectedWallet) ?? Wallets.FirstOrDefault();

		if (walletToSelect is { })
		{
			SelectedWallet = walletToSelect;
		}
	}

	public async Task InitialiseAsync()
	{
		var topItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Top);

		var bottomItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Bottom);

		foreach (var item in topItems)
		{
			var viewModel = await NavigationManager.MaterialiseViewModelAsync(item);

			if (viewModel is NavBarItemViewModel navBarItem)
			{
				TopItems.Add(navBarItem);
			}
		}

		foreach (var item in bottomItems)
		{
			var viewModel = await NavigationManager.MaterialiseViewModelAsync(item);

			if (viewModel is NavBarItemViewModel navBarItem)
			{
				BottomItems.Add(navBarItem);
			}
		}
	}
}
