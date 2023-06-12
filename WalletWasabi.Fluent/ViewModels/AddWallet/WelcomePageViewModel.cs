using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Welcome")]
public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private int _selectedIndex;
	[AutoNotify] private string? _nextLabel;
	[AutoNotify] private bool _enableNextKey = true;

	public WelcomePageViewModel(UiContext uiContext)
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		
		CanGoBack = this.WhenAnyValue(x => x.SelectedIndex, x => x > 0);
		CanGoNext = this.WhenAnyValue(x => x.SelectedIndex, x => x.ItemCount, (x, y) => x < y -1);
		NextCommand = ReactiveCommand.Create(() => SelectedIndex++, CanGoNext);
		BackCommand = ReactiveCommand.Create(() => SelectedIndex--, CanGoBack);

		CreateWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletNamePage(WalletCreationOption.AddNewWallet));
		ConnectHardwareWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletNamePage(WalletCreationOption.ConnectToHardwareWallet));
		ImportWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletNamePage(WalletCreationOption.ImportWallet, ""));
		RecoverWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletNamePage(WalletCreationOption.RecoverWallet));
	}

	public IObservable<bool> CanGoNext { get; }
	public IObservable<bool> CanGoBack { get; }

	[AutoNotify] private int _itemCount;
	public ICommand RecoverWalletCommand { get; }
	public ICommand ImportWalletCommand { get; }
	public ICommand ConnectHardwareWalletCommand { get; }
	public ICommand CreateWalletCommand { get; }
}
