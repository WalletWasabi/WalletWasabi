using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
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

				RxApp.MainThreadScheduler.Schedule(
					async () =>
					{
						await _backendInitialiseAsync!(); // Guaranteed not to be null when not in designer.

						MainViewModel.Instance.Initialize();
					});
			}
		}

		base.OnFrameworkInitializationCompleted();
	}

	private static IWalletRepository CreateWalletRepository()
	{
		if (Services.WalletManager is { })
		{
			return new WalletRepository();
		}
		else
		{
			return new NullWalletRepository();
		}
	}

	private static IHardwareWalletInterface CreateHardwareWalletInterface()
	{
		if (Services.WalletManager is { })
		{
			return new HardwareWalletInterface();
		}
		else
		{
			return new NullHardwareWalletInterface();
		}
	}

	private static IFileSystem CreateFileSystem()
	{
		if (Services.DataDir is { })
		{
			return new FileSystemModel();
		}
		else
		{
			return new NullFileSystem();
		}
	}

	private static IClientConfig CreateConfig()
	{
		if (Services.PersistentConfig is { })
		{
			return new ClientConfigModel();
		}
		else
		{
			return new NullClientConfig();
		}
	}

	private static IApplicationSettings CreateApplicationSettings()
	{
		if (Services.PersistentConfig is { } persistentConfig && Services.UiConfig is { } uiConfig)
		{
			return new ApplicationSettings(persistentConfig, uiConfig);
		}
		else
		{
			return new NullApplicationSettings();
		}
	}

	private UiContext CreateUiContext()
	{
		// This class (App) represents the actual Avalonia Application and it's sole presence means we're in the actual runtime context (as opposed to unit tests)
		// Once all ViewModels have been refactored to recieve UiContext as a constructor parameter, this static singleton property can be removed.
		return new UiContext(
			new QrGenerator(),
			new QrCodeReader(),
			new UiClipboard(),
			CreateWalletRepository(),
			CreateHardwareWalletInterface(),
			CreateFileSystem(),
			CreateConfig(),
			CreateApplicationSettings());
	}
}
