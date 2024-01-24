using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class CopyableItem : ContentControl
{
	public static readonly StyledProperty<string?> ContentToCopyProperty = AvaloniaProperty.Register<CopyableItem, string?>(nameof(ContentToCopy));

	public string? ContentToCopy
	{
		get => GetValue(ContentToCopyProperty);
		set => SetValue(ContentToCopyProperty, value);
	}
}
