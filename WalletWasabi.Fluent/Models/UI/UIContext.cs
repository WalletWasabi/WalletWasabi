using Avalonia;
using Avalonia.Input.Platform;

namespace WalletWasabi.Fluent.Models.UI;

public class UIContext
{
	public UIContext(IQrCodeGenerator qrCodeGenerator, IClipboard clipboard)
	{
		QrCodeGenerator = qrCodeGenerator ?? throw new ArgumentNullException(nameof(qrCodeGenerator));
		Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
	}

	public IClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }

	// The use of this property is a temporary workaround until we finalize the refactoring of all ViewModels (to be testable)
	// Application.Current should never be null
	public static UIContext Default => new(new QrGenerator(), Application.Current?.Clipboard!);
}
