using System;
using Avalonia;
using Avalonia.Input.Platform;
using WalletWasabi.Fluent.Models.ClientConfig;
using WalletWasabi.Fluent.Models.FileSystem;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;

	public UiContext(IQrCodeGenerator qrCodeGenerator, IQrCodeReader qrCodeReader, IUiClipboard clipboard, IWalletRepository walletRepository, IHardwareWalletInterface hardwareWalletInterface, IFileSystem fileSystem, IClientConfig config, IApplicationSettings applicationSettings)

	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		QrCodeReader = qrCodeReader ?? throw new ArgumentNullException(nameof(qrCodeReader));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		WalletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
		HardwareWalletInterface = hardwareWalletInterface ?? throw new ArgumentNullException(nameof(hardwareWalletInterface));
		FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		Config = config ?? throw new ArgumentNullException(nameof(config));
		ApplicationSettings = applicationSettings ?? throw new ArgumentNullException(nameof(applicationSettings));
	}

	public IUiClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }
	public IWalletRepository WalletRepository { get; }
	public IQrCodeReader QrCodeReader { get; }
	public IHardwareWalletInterface HardwareWalletInterface { get; }
	public IFileSystem FileSystem { get; }
	public IClientConfig Config { get; }
	public IApplicationSettings ApplicationSettings { get; }

	// The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be testable)

	// We provide a NullClipboard object for unit tests (when Application.Current is null)
	public static UiContext Default;

	public void RegisterNavigation(INavigate navigate)
	{
		_navigate ??= navigate;
	}

	public INavigate Navigate()
	{
		return _navigate ?? throw new InvalidOperationException($"{GetType().Name} {nameof(_navigate)} hasn't been initialized.");
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return
			_navigate?.Navigate(target)
			?? throw new InvalidOperationException($"{GetType().Name} {nameof(_navigate)} hasn't been initialized.");
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
}
