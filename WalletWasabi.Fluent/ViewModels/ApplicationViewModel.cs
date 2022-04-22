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
using WalletWasabi.Fluent.ViewModels.Dialogs;
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

		QuitCommand = ReactiveCommand.Create(_mainWindowService.Shutdown);

		ShowHideCommand = ReactiveCommand.Create(() =>
		{
			if (IsMainWindowShown)
			{
				_mainWindowService.Close();
			}
			else
			{
				_mainWindowService.Show();
			}
		});

		ShowCommand = ReactiveCommand.Create(() =>
		{
			_mainWindowService.Show();
		});

		AboutCommand = ReactiveCommand.Create(
			() => MainViewModel.Instance.DialogScreen.To(new AboutViewModel(navigateBack: MainViewModel.Instance.DialogScreen.CurrentPage is not null)),
			canExecute: MainViewModel.Instance.DialogScreen.WhenAnyValue(x => x.CurrentPage)
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
	public ICommand AboutCommand { get; }
	public ICommand ShowCommand { get; }

	public ICommand ShowHideCommand { get; }

	public ICommand QuitCommand { get; }

	public void OnShutdownPrevented()
	{
		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(new ShowErrorDialogViewModel (
				"Wasabi is currently anonymising your wallet. Please try again in a few minutes.",
				"Warning",
				"Unable to close right now"));
		});
	}

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