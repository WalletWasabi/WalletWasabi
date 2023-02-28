using Avalonia;
using Avalonia.Input.Platform;

namespace WalletWasabi.Fluent.Models.UI;

public class UIContext
{
	public UIContext(IQrCodeGenerator qrCodeGenerator, IClipboard clipboard)
	{
		QrCodeGenerator = qrCodeGenerator;
		Clipboard = clipboard;
	}

	public IClipboard Clipboard { get; }
	public IQrCodeGenerator QrCodeGenerator { get; }

	public static UIContext Default => new(new QrGenerator(), Application.Current?.Clipboard);
}
