using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;
public class CopyableItem : HeaderedContentControl
{
	public static readonly StyledProperty<string> TextualValueProperty = AvaloniaProperty.Register<CopyableItem, string>("TextualValue");

	public string TextualValue
	{
		get => GetValue(TextualValueProperty);
		set => SetValue(TextualValueProperty, value);
	}
}
