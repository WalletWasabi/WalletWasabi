using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels;

public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
{
	[AutoNotify] private string _showOrHideHeader;

	public ApplicationViewModel()
	{
		QuitCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (CanShutdown())
			{
				if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
					desktopLifetime)
				{
					desktopLifetime.Shutdown();
				}
			}
			else
			{
					// Show the window if it was hidden.
					ShowRequested?.Invoke(this, EventArgs.Empty);

				await MainViewModel.Instance!.CompactDialogScreen.NavigateDialogAsync(new Dialogs.ShowErrorDialogViewModel(
					"Wasabi is currently anonymising your wallet. Please try again in a few minutes.",
					"Warning",
					"Unable to close right now"));
			}
		});

		ShowCommand = ReactiveCommand.Create(() => ShowRequested?.Invoke(this, EventArgs.Empty));

		TrayIcon = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
			? new WindowIcon(AssetHelpers.GetBitmapAsset("avares://WalletWasabi.Fluent/Assets/WasabiLogo_white.ico"))
			: new WindowIcon(AssetHelpers.GetBitmapAsset("avares://WalletWasabi.Fluent/Assets/WasabiLogo.ico"));
	}

	public WindowIcon TrayIcon { get; }

	public event EventHandler? ShowRequested;

	public ICommand QuitCommand { get; }
	public ICommand ShowCommand { get; }

	public bool CanShutdown()
	{
		var cjManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		if (cjManager is { })
		{
			return cjManager.HighestCoinJoinClientState switch
			{
				CoinJoinClientState.InCriticalPhase => false,
				CoinJoinClientState.Idle or CoinJoinClientState.InProgress => true,
				_ => throw new ArgumentOutOfRangeException(),
			};
		}

		return true;
	}

	public void MainViewInitializedFromDesktop()
	{
		MainViewModel.Instance!.WhenAnyValue(x => x.WindowState)
			.Subscribe(x =>
			{
				ShowOrHideHeader = x is WindowState.Maximized or WindowState.FullScreen ? "Hide" : "Show";
			});
	}
}
