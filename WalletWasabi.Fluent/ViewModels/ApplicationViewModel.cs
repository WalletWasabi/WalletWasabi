using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels;

public class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
{
	public ApplicationViewModel()
	{
		QuitCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (CanShutdown())
			{
				if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
				{
					lifetime.Shutdown();
				}
			}
			else
			{
				// Show the window if it was hidden.
				ShowRequested?.Invoke(this, EventArgs.Empty);

				await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(new Dialogs.ShowErrorDialogViewModel(
					"Wasabi is currently anonymising your wallet. Please try again in a few minutes.",
					"Warning",
					"Unable to close right now"));
			}
		});

		ShowCommand = ReactiveCommand.Create(() => ShowRequested?.Invoke(this, EventArgs.Empty));

		var dialogScreen = MainViewModel.Instance.DialogScreen;

		AboutCommand = ReactiveCommand.Create(
			() => dialogScreen.To(new AboutViewModel(navigateBack: dialogScreen.CurrentPage is not null)),
			canExecute: dialogScreen.WhenAnyValue(x => x.CurrentPage)
				.SelectMany(x =>
				{
					return x switch
					{
						null => Observable.Return(true),
						AboutViewModel => Observable.Return(false),
						_ => x.WhenAnyValue(page => page.IsBusy).Select(isBusy => !isBusy)
					};
				}));

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			using var bitmap = AssetHelpers.GetBitmapAsset("avares://WalletWasabi.Fluent/Assets/WasabiLogo_white.ico");

			TrayIcon = new WindowIcon(bitmap);
		}
		else
		{
			using var bitmap = AssetHelpers.GetBitmapAsset("avares://WalletWasabi.Fluent/Assets/WasabiLogo.ico");

			TrayIcon = new WindowIcon(bitmap);
		}
	}

	public WindowIcon TrayIcon { get; }

	public event EventHandler? ShowRequested;

	public ICommand AboutCommand { get; }
	public ICommand ShowCommand { get; }
	public ICommand QuitCommand { get; }

	public bool CanShutdown()
	{
		var cjManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		if (cjManager is { })
		{
			if (cjManager.IsAnyCoinJoinInCriticalPhase)
			{
				return false;
			}
		}

		return true;
	}
}
