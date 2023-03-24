using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.NavBar;

/// <summary>
/// The ViewModel that represents the structure of the sidebar.
/// </summary>
public class NavBarViewModel : ViewModelBase
{
	public NavBarViewModel()
	{
		TopItems = new ObservableCollection<NavBarItemViewModel>();
		BottomItems = new ObservableCollection<NavBarItemViewModel>();

		SetDefaultSelection();

		Observable.Merge(
				WhenItemSelected(Wallets.ToObservableChangeSet().Transform(x => x as NavBarItemViewModel)),
				WhenItemSelected(BottomItems.ToObservableChangeSet()),
				WhenItemSelected(TopItems.ToObservableChangeSet()))
			.Buffer(2, 1)
			.Select(buffer => (OldValue: buffer[0], NewValue: buffer[1]))
			.Subscribe(x =>
			{
				if (x.OldValue is { } old)
				{
					old.IsSelected = false;
				}

				if (x.NewValue is WalletViewModelBase wallet)
				{
					Services.UiConfig.LastSelectedWallet = wallet.WalletName;
				}
			});
	}

	public ObservableCollection<NavBarItemViewModel> TopItems { get; }

	public ObservableCollection<NavBarItemViewModel> BottomItems { get; }

	public ObservableCollection<WalletViewModelBase> Wallets => UiServices.WalletManager.Wallets;

	private IObservable<NavBarItemViewModel> WhenItemSelected(IObservable<IChangeSet<NavBarItemViewModel>> observable)
	{
		return observable
			.WhenPropertyChanged(x => x.IsSelected)
			.Where(x => x.Value)
			.Select(x => x.Sender);
	}

	private void SetDefaultSelection()
	{
		var walletToSelect = Wallets.FirstOrDefault(item => item.WalletName == Services.UiConfig.LastSelectedWallet) ?? Wallets.FirstOrDefault();

		if (walletToSelect is { } && walletToSelect.OpenCommand.CanExecute(default))
		{
			walletToSelect.OpenCommand.Execute(default);
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
