using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Announcements;
using WalletWasabi.Client;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Logging;

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

	public Func<Task>? BackendInitializeAsync { get; set; }

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (!Design.IsDesignMode && ApplicationLifetime is not null)
		{
#if USE_CDP
			if (System.Environment.GetEnvironmentVariable("WASABI_USE_CDP") == "1")
			{
				try
				{
					Avalonia.Diagnostics.Cdp.CdpServer.EnsureInitialized();
					Avalonia.Diagnostics.Cdp.CdpServer.Start(9222);
					Logger.LogInfo("CDP Server started on port 9222");
				}
				catch (Exception ex)
				{
					Logger.LogError("Failed to start CDP Server", ex);
				}
			}
#endif

			var uiContext = CreateUiContext();
			var mainViewModel = new MainViewModel(uiContext);
			_applicationStateManager = new ApplicationStateManager(ApplicationLifetime, uiContext, mainViewModel, _startInBg);
			var applicationViewModel = _applicationStateManager.ApplicationViewModel;
			DataContext = applicationViewModel;

			WalletWasabi.Fluent.Helpers.MobileAutomation.Start(mainViewModel);

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
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
			else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
			{
#if USE_CDP
				if (System.Environment.GetEnvironmentVariable("WASABI_USE_CDP") == "1")
				{
					void TryRegister()
					{
						try
						{
							var topLevel = TopLevel.GetTopLevel(single.MainView);
							if (topLevel != null)
							{
								Avalonia.Diagnostics.Cdp.CdpServer.Register(topLevel, "Wasabi Wallet Mobile");
								Logger.LogInfo("Registered single view TopLevel with CDP Server");
							}
						}
						catch (Exception ex)
						{
							Logger.LogError("Failed to register single view TopLevel with CDP Server", ex);
						}
					}

					if (single.MainView != null)
					{
						var initialTopLevel = TopLevel.GetTopLevel(single.MainView);
						if (initialTopLevel != null)
						{
							TryRegister();
						}
						else
						{
							single.MainView.AttachedToVisualTree += (s, e) => TryRegister();
						}
					}
				}
#endif

				Avalonia.Threading.Dispatcher.UIThread.Post(
					async () =>
					{
						try
						{
							if (BackendInitializeAsync is not null)
							{
								await BackendInitializeAsync();
							}
							else if (_backendInitializeAsync is not null)
							{
								await _backendInitializeAsync();
							}
						}
						catch (Exception ex)
						{
							Logger.LogError("BackendInitializeAsync failed", ex);
						}

						mainViewModel.Initialize();
					});
			}
		}

		base.OnFrameworkInitializationCompleted();
#if DEBUG
		if (CanRunDevTools())
		{
			this.AttachDevTools();
		}
#endif
	}

	public static AppBuilder InitializeMobile(WasabiApplication app, AppBuilder appBuilder)
	{
		RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
		{
			if (Debugger.IsAttached)
			{
				Debugger.Break();
			}
			Logger.LogError(ex);
		});

		Logger.LogInfo("Wasabi Mobile GUI started.");
		UiConfig uiConfig = LoadOrCreateUiConfig(Config.DataDir);
		var services = Services.Create(app.Global, uiConfig, app.TerminateService);
		
		appBuilder = appBuilder
			.AfterSetup(b =>
			{
				ThemeHelper.ApplyTheme(uiConfig.DarkModeEnabled ? Theme.Dark : Theme.Light);
				if (Application.Current is App a)
				{
					a.BackendInitializeAsync = async () =>
					{
						using CancellationTokenSource stopLoadingCts = new();
						await app.Global.InitializeAsync(initializeSleepInhibitor: false, app.TerminateService, stopLoadingCts.Token).ConfigureAwait(false);
						await StartupHelper.ModifyStartupSettingAsync(uiConfig.RunOnSystemStartup).ConfigureAwait(false);
					};
				}
			});

		return appBuilder;
	}

	private static UiConfig LoadOrCreateUiConfig(string dataDir)
	{
		Directory.CreateDirectory(dataDir);
		return UiConfig.LoadFile(Path.Combine(dataDir, "UiConfig.json"));
	}

	private bool CanRunDevTools()
	{
		return !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();
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
