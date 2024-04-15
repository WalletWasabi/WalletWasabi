using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class SubActionButton : SplitButton
{
	public static readonly StyledProperty<StreamGeometry> IconProperty = AvaloniaProperty.Register<SubActionButton, StreamGeometry>("Icon");

	public StreamGeometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}
}
