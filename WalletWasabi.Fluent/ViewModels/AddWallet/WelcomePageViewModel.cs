using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Welcome")]
public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
{
	private const int NumberOfPages = 5;
	private readonly AddWalletPageViewModel _addWalletPage;
	[AutoNotify] private int _selectedIndex;
	[AutoNotify] private string? _nextLabel;
	[AutoNotify] private bool _enableNextKey = true;

	public WelcomePageViewModel(AddWalletPageViewModel addWalletPage)
	{
		_addWalletPage = addWalletPage;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		SelectedIndex = 0;
		NextCommand = ReactiveCommand.Create(OnNext);

		this.WhenAnyValue(x => x.SelectedIndex)
			.Subscribe(x =>
			{
				NextLabel = x < NumberOfPages - 1 ? "Continue" : "Get Started";
				EnableNextKey = x < NumberOfPages - 1;
			});

		this.WhenAnyValue(x => x.IsActive)
			.Skip(1)
			.Where(x => !x)
			.Subscribe(x => EnableNextKey = false);
	}

	private void OnNext()
	{
		if (SelectedIndex < NumberOfPages - 1)
		{
			SelectedIndex++;
		}
		else if (!Services.WalletManager.HasWallet())
		{
			Navigate().To(_addWalletPage);
		}
		else
		{
			Close();
		}
	}
}
