using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;
public class CopyableContentControl : ContentControl
{
	public static readonly StyledProperty<string> TextualValueProperty = AvaloniaProperty.Register<CopyableContentControl, string>("TextualValue");

	public string TextualValue
	{
		get => GetValue(TextualValueProperty);
		set => SetValue(TextualValueProperty, value);
	}
}
