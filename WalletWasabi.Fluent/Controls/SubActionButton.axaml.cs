using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class SubActionButton : SplitButton
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
		AvaloniaProperty.Register<SubActionButton, IEnumerable?>(nameof(ItemsSource));

	public IEnumerable? ItemsSource
	{
		get => GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public static readonly StyledProperty<StreamGeometry> IconProperty = AvaloniaProperty.Register<SubActionButton, StreamGeometry>("Icon");

	public StreamGeometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}
}
