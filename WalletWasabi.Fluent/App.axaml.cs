using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent;

public class App : Application
{
	private readonly bool _startInBg;
	private readonly string? _bip21Uri;
	private readonly Func<Task>? _backendInitialiseAsync;
	private ApplicationStateManager? _applicationStateManager;
	private Bip21Workflow? _bip21Workflow;

	public App()
	{
		Name = "Wasabi Wallet";
	}

	public App(Func<Task> backendInitialiseAsync, bool startInBg, string? bip21Uri) : this()
	{
		_bip21Uri = bip21Uri;
		_startInBg = startInBg;
		_backendInitialiseAsync = backendInitialiseAsync;
	}

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (!Design.IsDesignMode)
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var uiContext = CreateUiContext();
				
				_applicationStateManager = new ApplicationStateManager(desktop, uiContext, _startInBg);
				_bip21Workflow = new Bip21Workflow(uiContext, MainViewModel.Instance.CurrentWallet);
				_bip21Workflow.RegisterUriHandler();

				DataContext = _applicationStateManager.ApplicationViewModel;

				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						await _backendInitialiseAsync!(); // Guaranteed not to be null when not in designer.

						MainViewModel.Instance.Initialize();

						if (_bip21Uri is { })
						{
							_bip21Workflow.HandleUri(_bip21Uri);
						}
					});
			}
		}

		base.OnFrameworkInitializationCompleted();
	}

	private UiContext CreateUiContext()
	{
		// TODO: This method is really the place where UiContext should be instantiated, as opposed to using a static singleton.
		// This class (App) represents the actual Avalonia Application and it's sole presence means we're in the actual runtime context (as opposed to unit tests)
		// Once all ViewModels have been refactored to recieve UiContext as a constructor parameter, this static singleton property can be removed.
		return UiContext.Default;
	}
}
