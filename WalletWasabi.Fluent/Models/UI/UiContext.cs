using Avalonia;
using Avalonia.Input.Platform;

namespace WalletWasabi.Fluent.Models.UI;

public class UiContext
{
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
}
