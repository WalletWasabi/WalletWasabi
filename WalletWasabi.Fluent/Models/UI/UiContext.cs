using Avalonia;
using Avalonia.Input.Platform;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;

	public UiContext(IQrCodeGenerator qrCodeGenerator, IClipboard clipboard)
	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
	}

	public IClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }

	// The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be testable)
	// Application.Current should never be null
	public static UiContext Default => new(new QrGenerator(), Application.Current?.Clipboard!);

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
