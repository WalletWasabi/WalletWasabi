using Avalonia;
using Avalonia.Input.Platform;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;
	private static UiContext? DefaultInstance;

	public UiContext(IQrCodeGenerator qrCodeGenerator, IQrCodeReader qrCodeReader, IClipboard clipboard, IWalletRepository walletRepository, IHardwareWalletInterface hardwareWalletInterface)
	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		QrCodeReader = qrCodeReader ?? throw new ArgumentNullException(nameof(qrCodeReader));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		WalletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
		HardwareWalletInterface = hardwareWalletInterface ?? throw new ArgumentNullException(nameof(hardwareWalletInterface));
	}

	public IClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }
	public IWalletRepository WalletRepository { get; }
	public IQrCodeReader QrCodeReader { get; }
	public IHardwareWalletInterface HardwareWalletInterface { get; }

	// The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be testable)
	// We provide a NullClipboard object for unit tests (when Application.Current is null)
	public static UiContext Default => DefaultInstance ??= new UiContext(new QrGenerator(), new QrCodeReader(), ApplicationHelper.Clipboard ?? new NullClipboard(), CreateWalletRepository(), CreateHardwareWalletInterface());

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
}
