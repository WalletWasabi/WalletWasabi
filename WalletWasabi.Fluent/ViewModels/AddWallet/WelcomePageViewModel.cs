using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

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

		CreateWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletName(WalletCreationOption.AddNewWallet));
		ConnectHardwareWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletNamePage(WalletCreationOption.ConnectToHardwareWallet));
		ImportWalletCommand = ReactiveCommand.CreateFromTask(OnImportWalletAsync);
		RecoverWalletCommand = ReactiveCommand.Create(() => uiContext.Navigate().To().WalletNamePage(WalletCreationOption.RecoverWallet));
	}

	public IObservable<bool> CanGoNext { get; }
	public IObservable<bool> CanGoBack { get; }
	public ICommand RecoverWalletCommand { get; }
	public ICommand ImportWalletCommand { get; }
	public ICommand ConnectHardwareWalletCommand { get; }
	public ICommand CreateWalletCommand { get; }

	private async Task OnImportWalletAsync()
	{
		try
		{
			var filePath = await FileDialogHelper.ShowOpenFileDialogAsync("Import wallet file", new[] { "json" });

			if (filePath is null)
			{
				return;
			}

			var walletName = Path.GetFileNameWithoutExtension(filePath);

			var validationError = WalletHelpers.ValidateWalletName(walletName);
			if (validationError is { })
			{
				Navigate().To().WalletNamePage(WalletCreationOption.ImportWallet, filePath);
				return;
			}

			var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, walletName, filePath);

			// TODO: Remove this after current ViewModel is decoupled
			var walletSettings = new WalletSettingsModel(keyManager, true);

			Navigate().To().AddedWalletPage(walletSettings);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Import wallet", ex.ToUserFriendlyString(), "Wasabi was unable to import your wallet.");
		}
	}
}
