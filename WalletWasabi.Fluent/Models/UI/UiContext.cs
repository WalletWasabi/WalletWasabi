using Avalonia;
using Avalonia.Input.Platform;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;
	private static UiContext? DefaultInstance;

	public UiContext(IQrCodeGenerator qrCodeGenerator, IClipboard clipboard, IWalletListModel walletList)
	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		WalletList = walletList ?? throw new ArgumentNullException(nameof(walletList));
	}

	public IClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }
	public IWalletListModel WalletList { get; }

	// The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be testable)
	// We provide a NullClipboard object for unit tests (when Application.Current is null)
	public static UiContext Default => DefaultInstance ??= new UiContext(new QrGenerator(), Application.Current?.Clipboard ?? new NullClipboard(), new WalletListModel());

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
}
