using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NBitcoin;
using WalletWasabi.Announcements;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent;

public class App : Application
{
	private readonly bool _startInBg;
	private readonly UiConfig _uiConfig;
	private readonly IServices _services;
	private readonly Func<Task>? _backendInitializeAsync;
	private ApplicationStateManager? _applicationStateManager;

	public App()
	{
		Name = "Wasabi Wallet";
	}

	public App(UiConfig uiConfig, IServices services, Func<Task> backendInitializeAsync, bool startInBg) : this()
	{
		_uiConfig = uiConfig;
		_services = services;
		_startInBg = startInBg;
		_backendInitializeAsync = backendInitializeAsync;
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
				var mainViewModel = new MainViewModel(uiContext);
				_applicationStateManager = new ApplicationStateManager(desktop, uiContext, mainViewModel, _startInBg);
				var applicationViewModel = _applicationStateManager.ApplicationViewModel;
				DataContext = applicationViewModel;

				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
				desktop.Exit += (sender, args) =>
				{
					mainViewModel.ClearStacks();
					uiContext.HealthMonitor.Dispose();
				};

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						await _backendInitializeAsync!(); // Guaranteed not to be null when not in designer.

						mainViewModel.Initialize();
					});

				InitializeTrayIcons();
			}
		}

		base.OnFrameworkInitializationCompleted();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private void InitializeTrayIcons()
	{
		// TODO: This is temporary workaround until https://github.com/WalletWasabi/WalletWasabi/issues/8151 is fixed.
		var trayIcons = TrayIcon.GetIcons(this);
		if (trayIcons is not null && trayIcons.FirstOrDefault() is { } trayIcon)
		{
			if (this.TryFindResource("DefaultNativeMenu", out var nativeMenu))
			{
				trayIcon.Menu = nativeMenu as NativeMenu;
			}
		}
	}

	private UiContext CreateUiContext()
	{
		var amountProvider = new AmountProvider(_services);

		var applicationSettings = new ApplicationSettings(_services, ((Services)_services).PersistentConfig, ((Services)_services).Config, _uiConfig);
		var torStatusChecker = new TorStatusCheckerModel(_services);

		// This class (App) represents the actual Avalonia Application and it's sole presence means we're in the actual runtime context (as opposed to unit tests)
		// Once all ViewModels have been refactored to receive UiContext as a constructor parameter, this static singleton property can be removed.
		return new UiContext(
			_services,
			_uiConfig,
			new QrCodeGenerator(),
			new QrCodeReader(),
			new UiClipboard(),
			new WalletRepository(_services, _uiConfig, amountProvider),
			new CoinjoinModel(_services),
			new HardwareWalletInterface(_services),
			new FileSystemModel(),
			new ClientConfigModel(_services),
			applicationSettings,
			new TransactionBroadcasterModel(_services, applicationSettings.Network),
			amountProvider,
			new EditableSearchSource(),
			torStatusChecker,
			new HealthMonitor(_services, torStatusChecker),
			new ReleaseHighlights(),
			_services.GetScheme());
	}
}
