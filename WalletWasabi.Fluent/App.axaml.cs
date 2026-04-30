using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Announcements;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent;

public class App : Application
{
	private readonly bool _startInBg;
	private readonly Func<Task>? _backendInitializeAsync;
	private ApplicationStateManager? _applicationStateManager;

	public App()
	{
		Name = "Wasabi Wallet";
	}

	public App(Func<Task> backendInitializeAsync, bool startInBg) : this()
	{
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

	private static WalletRepository CreateWalletRepository(IServices services, AmountProvider amountProvider)
	{
		return new WalletRepository(services, amountProvider);
	}

	private static HardwareWalletInterface CreateHardwareWalletInterface(IServices services)
	{
		return new HardwareWalletInterface(services);
	}

	private static FileSystemModel CreateFileSystem()
	{
		return new FileSystemModel();
	}

	private static ClientConfigModel CreateConfig(IServices services)
	{
		return new ClientConfigModel(services);
	}

	private static ApplicationSettings CreateApplicationSettings(IServices services)
	{
		return new ApplicationSettings(services, services.PersistentConfig, services.Config, services.UiConfig);
	}

	private static TransactionBroadcasterModel CreateBroadcaster(IServices services, Network network)
	{
		return new TransactionBroadcasterModel(services, network);
	}

	private static AmountProvider CreateAmountProvider(IServices services)
	{
		return new AmountProvider(services);
	}

	private UiContext CreateUiContext()
	{
		var services = Services.Instance;
		var amountProvider = CreateAmountProvider(services);

		var applicationSettings = CreateApplicationSettings(services);
		var torStatusChecker = new TorStatusCheckerModel(services);

		// This class (App) represents the actual Avalonia Application and it's sole presence means we're in the actual runtime context (as opposed to unit tests)
		// Once all ViewModels have been refactored to receive UiContext as a constructor parameter, this static singleton property can be removed.
		return new UiContext(
			services,
			new QrCodeGenerator(),
			new QrCodeReader(),
			new UiClipboard(),
			CreateWalletRepository(services, amountProvider),
			new CoinjoinModel(services),
			CreateHardwareWalletInterface(services),
			CreateFileSystem(),
			CreateConfig(services),
			applicationSettings,
			CreateBroadcaster(services, applicationSettings.Network),
			amountProvider,
			new EditableSearchSource(),
			torStatusChecker,
			new HealthMonitor(services, torStatusChecker),
			new ReleaseHighlights(),
			services.Scheme);
	}
}
