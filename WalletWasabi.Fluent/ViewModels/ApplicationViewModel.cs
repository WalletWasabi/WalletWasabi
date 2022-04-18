using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
{
	private readonly IMainWindowService _mainWindowService;
	[AutoNotify] private bool _isMainWindowShown = true;

	public ApplicationViewModel(IMainWindowService mainWindowService)
	{
		_mainWindowService = mainWindowService;

		this.WhenAnyValue(x => x.IsMainWindowShown)
			.Where(x => x == false)
			.Subscribe(async x =>
			{
				if (!Services.UiConfig.HideOnClose)
				{
					DoQuit();
				}
			});

		QuitCommand = ReactiveCommand.Create(DoQuit);

		ShowCommand = ReactiveCommand.Create(() =>
		{
			if (IsMainWindowShown)
			{
				_mainWindowService.Close();
				IsMainWindowShown = false;
			}
			else
			{
				_mainWindowService.Show();
				IsMainWindowShown = true;
			}
		});

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

	private void DoQuit()
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
			_mainWindowService.Show();
			IsMainWindowShown = true;

			OnClosePrevented();
		}
	}

	public void OnClosePrevented()
	{
		_mainWindowService.Show();

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(new Dialogs.ShowErrorDialogViewModel(
				"Wasabi is currently anonymising your wallet. Please try again in a few minutes.",
				"Warning",
				"Unable to close right now"));
		});
	}

	public WindowIcon TrayIcon { get; }
	public ICommand AboutCommand { get; }
	public ICommand ShowCommand { get; }
	public ICommand QuitCommand { get; }

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
}