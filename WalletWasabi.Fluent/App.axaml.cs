using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent;

public class App : Application
{
	private readonly bool _startInBg;
	private readonly Func<Task>? _backendInitialiseAsync;
	private ApplicationStateManager? _applicationStateManager;

	public App()
	{
		Name = "Wasabi Wallet";
	}

	public App(Func<Task> backendInitialiseAsync, bool startInBg) : this()
	{
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
				UiContext.Default = uiContext;
				_applicationStateManager =
					new ApplicationStateManager(desktop, uiContext, _startInBg);

				DataContext = _applicationStateManager.ApplicationViewModel;

				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
				desktop.Exit += (sender, args) =>
				{
					MainViewModel.Instance.ClearStacks();
					MainViewModel.Instance.StatusIcon.Dispose();
				};

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						await _backendInitialiseAsync!(); // Guaranteed not to be null when not in designer.

						MainViewModel.Instance.Initialize();
					});
			}
		}

		base.OnFrameworkInitializationCompleted();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private static IWalletRepository CreateWalletRepository()
	{
		return new WalletRepository();
	}

	private static IHardwareWalletInterface CreateHardwareWalletInterface()
	{
		return new HardwareWalletInterface();
	}

	private static IFileSystem CreateFileSystem()
	{
		return new FileSystemModel();
	}

	private static IClientConfig CreateConfig()
	{
		return new ClientConfigModel();
	}

	private static IApplicationSettings CreateApplicationSettings()
	{
		return new ApplicationSettings(Services.PersistentConfig, Services.UiConfig);
	}

	private UiContext CreateUiContext()
	{
		// This class (App) represents the actual Avalonia Application and it's sole presence means we're in the actual runtime context (as opposed to unit tests)
		// Once all ViewModels have been refactored to receive UiContext as a constructor parameter, this static singleton property can be removed.
		return new UiContext(
			new QrCodeGenerator(),
			new QrCodeReader(),
			new UiClipboard(),
			CreateWalletRepository(),
			CreateHardwareWalletInterface(),
			CreateFileSystem(),
			CreateConfig(),
			CreateApplicationSettings());
	}
}
