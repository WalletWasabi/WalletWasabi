using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Welcome")]
public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
{
	private const int NumberOfPages = 2;
	private readonly AddWalletPageViewModel _addWalletPage;
	[AutoNotify] private int _selectedIndex;
	[AutoNotify] private string? _nextLabel;
	[AutoNotify] private bool _enableNextKey = true;

	public WelcomePageViewModel(AddWalletPageViewModel addWalletPage)
	{
		_addWalletPage = addWalletPage;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		
		CanGoBack = this.WhenAnyValue(x => x.SelectedIndex, x => x > 0);
		CanGoNext = this.WhenAnyValue(x => x.SelectedIndex, x => x.ItemCount, (x, y) => x < y -1);
		NextCommand = ReactiveCommand.Create(() => SelectedIndex++, CanGoNext);
		BackCommand = ReactiveCommand.Create(() => SelectedIndex--, CanGoBack);
	}

	public IObservable<bool> CanGoNext { get; }
	public IObservable<bool> CanGoBack { get; }

	[AutoNotify] private int _itemCount;
	public ICommand RecoverWalletCommand { get; }
	public ICommand ImportWalletCommand { get; }
	public ICommand ConnectHardwareWalletCommand { get; }
	public ICommand CreateWalletCommand { get; }
}
