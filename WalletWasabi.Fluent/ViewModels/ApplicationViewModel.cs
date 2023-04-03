using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Providers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels;

public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
{
	private readonly IMainWindowService _mainWindowService;
	[AutoNotify] private bool _isMainWindowShown = true;

	public ApplicationViewModel(IMainWindowService mainWindowService)
	{
		_mainWindowService = mainWindowService;

		QuitCommand = ReactiveCommand.Create(() => Shutdown(false));

		ShowHideCommand = ReactiveCommand.Create(() =>
		{
			if (IsMainWindowShown)
			{
				_mainWindowService.Hide();
			}
			else
			{
				_mainWindowService.Show();
			}
		});

		ShowCommand = ReactiveCommand.Create(() => _mainWindowService.Show());

		AboutCommand = ReactiveCommand.Create(AboutExecute, AboutCanExecute());

		using var bitmap = AssetHelpers.GetBitmapAsset("avares://WalletWasabi.Fluent/Assets/WasabiLogo.ico");
		TrayIcon = new WindowIcon(bitmap);
	}

	public WindowIcon TrayIcon { get; }
	public ICommand AboutCommand { get; }
	public ICommand ShowCommand { get; }

	public ICommand ShowHideCommand { get; }

	public ICommand QuitCommand { get; }

	private void AboutExecute()
	{
		MainViewModel.Instance.DialogScreen.To(
			new AboutViewModel(navigateBack: MainViewModel.Instance.DialogScreen.CurrentPage is not null));
	}

	private IObservable<bool> AboutCanExecute()
	{
		return MainViewModel.Instance.DialogScreen
			.WhenAnyValue(x => x.CurrentPage)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(x => x is null);
	}

	public void Shutdown(bool restart) => _mainWindowService.Shutdown(restart);

	public void OnShutdownPrevented(bool restartRequest)
	{
		MainViewModel.Instance.ApplyUiConfigWindowState(); // Will pop the window if it was minimized.

		if (!MainViewCanShutdown() && !restartRequest)
		{
			MainViewModel.Instance.ShowDialogAlert();
			return;
		}

		MainViewModel.Instance.CompactDialogScreen.To(new ShuttingDownViewModel(this, restartRequest));
	}

	public bool CanShutdown(bool restart)
	{
		if (!MainViewCanShutdown() && !restart)
		{
			return false;
		}

		return CoinJoinCanShutdown();
	}

	public bool MainViewCanShutdown()
	{
		return !MainViewModel.Instance.IsDialogOpen();
	}

	public bool CoinJoinCanShutdown()
	{
		var cjManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();

		if (cjManager is { })
		{
			return cjManager.HighestCoinJoinClientState switch
			{
				CoinJoinClientState.InCriticalPhase => false,
				CoinJoinClientState.Idle or CoinJoinClientState.InSchedule or CoinJoinClientState.InProgress => true,
				_ => throw new ArgumentOutOfRangeException(),
			};
		}

		return true;
	}
}
