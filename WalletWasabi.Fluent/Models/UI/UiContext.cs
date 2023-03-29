using Avalonia;
using Avalonia.Input.Platform;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
	private INavigate? _navigate;

	public UiContext(IQrCodeGenerator qrCodeGenerator, IClipboard clipboard)
	{
		QrCodeGenerator = qrCodeGenerator;
		Clipboard = clipboard;
	}

	public IClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }

	public static UiContext Default => new(new QrGenerator(), Application.Current?.Clipboard);

	public void RegisterNavigation(INavigate navigate)
	{
		_navigate ??= navigate;
	}

	public INavigate Navigate()
	{
		return _navigate ?? throw new InvalidOperationException("UIContext Navigation hasn't been initialized.");
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget target)
	{
		return
			_navigate?.Navigate(target)
			?? throw new InvalidOperationException("UIContext Navigation hasn't been initialized.");
	}
}
