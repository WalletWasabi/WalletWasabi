using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Welcome")]
public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private bool _enableNextKey = true;

	[AutoNotify] private int _itemCount;
	[AutoNotify] private string? _nextLabel;
	[AutoNotify] private int _selectedIndex;

	public WelcomePageViewModel(UiContext uiContext)
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		CanGoBack = this.WhenAnyValue(x => x.SelectedIndex, x => x > 0);
		CanGoNext = this.WhenAnyValue(x => x.SelectedIndex, x => x.ItemCount, (x, y) => x < y - 1);
		NextCommand = ReactiveCommand.Create(() => SelectedIndex++, CanGoNext);
		BackCommand = ReactiveCommand.Create(() => SelectedIndex--, CanGoBack);

		AddWallet = new AddWalletPageDialogViewModel(uiContext);
	}

	public AddWalletPageDialogViewModel AddWallet { get; }

	public IObservable<bool> CanGoNext { get; }
	public IObservable<bool> CanGoBack { get; }
}
