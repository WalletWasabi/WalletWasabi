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
		SetDefaultSelection();

		this.WhenAnyValue(x => x.SelectedWallet)
			.DistinctUntilChanged(x => x?.Title)
			.Do(x =>
			{
				if (x is not { })
				{
					return;
				}
				Services.UiConfig.LastSelectedWallet = x.Title;
				x.Navigate().To(x);
			})
			.Subscribe();
	}

	public ObservableCollection<WalletViewModelBase> Wallets => UiServices.WalletManager.Wallets;

	[AutoNotify] private WalletViewModelBase? _selectedWallet;

	private void SetDefaultSelection()
	{
		var walletToSelect = Wallets.FirstOrDefault(item => item.WalletName == Services.UiConfig.LastSelectedWallet) ??
		                     Wallets.FirstOrDefault();

		if (walletToSelect is { })
		{
			SelectedWallet = walletToSelect;
		}
	}
}
