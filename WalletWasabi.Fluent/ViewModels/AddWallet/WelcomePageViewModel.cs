using System.Reactive;
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

	public WelcomePageViewModel(AddWalletPageViewModel addWalletPage)
	{
		_addWalletPage = addWalletPage;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);
		EnableBack = false;
		
		SelectedIndex = 0;
		NextCommand = ReactiveCommand.Create(OnNext);

		this.WhenAnyValue(x => x.SelectedIndex)
			.Subscribe(x => NextLabel = x < NumberOfPages - 1 ? "Continue" : "Get Started");
	}

	public Bitmap WelcomeImage { get; } = AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WelcomeScreen/{ThemeHelper.CurrentTheme}/welcome.png");

	public Bitmap TrustlessImage { get; } = AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WelcomeScreen/{ThemeHelper.CurrentTheme}/trustless.png");

	public Bitmap OpensourceImage { get; } = AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WelcomeScreen/{ThemeHelper.CurrentTheme}/opensource.png");

	public Bitmap AnonymousImage { get; } = AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/WelcomeScreen/{ThemeHelper.CurrentTheme}/anonymous.png");

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
