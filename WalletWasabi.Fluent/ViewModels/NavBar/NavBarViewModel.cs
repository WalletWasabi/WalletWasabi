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
		BottomItems = new ObservableCollection<NavBarItemViewModel>();
		SetDefaultSelection();

		this.WhenAnyValue(x => x.SelectedWallet)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(x => x?.Activate())
			.Subscribe();
	}

	public ObservableCollection<NavBarItemViewModel> BottomItems { get; }

	public ObservableCollection<NavBarWalletStateViewModel> Wallets => UiServices.WalletManager.Wallets;

	[AutoNotify] private NavBarWalletStateViewModel _selectedWallet;

	private IObservable<NavBarItemViewModel> WhenItemSelected(IObservable<IChangeSet<NavBarItemViewModel>> observable)
	{
		return observable
			.WhenPropertyChanged(x => x.IsSelected)
			.Where(x => x.Value)
			.Select(x => x.Sender);
	}

	private void SetDefaultSelection()
	{
		var walletToSelect = Wallets.FirstOrDefault(item => item.Wallet.WalletName == Services.UiConfig.LastSelectedWallet) ?? Wallets.FirstOrDefault();
		//
		//if (walletToSelect is { } && walletToSelect.OpenCommand.CanExecute(default))
		//{
		//	walletToSelect.OpenCommand.Execute(default);
		//}

		walletToSelect?.Activate();
	}

	public async Task InitialiseAsync()
	{
		var bottomItems = NavigationManager.MetaData.Where(x => x.NavBarPosition == NavBarPosition.Bottom);

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
