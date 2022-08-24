using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class StatusItem : ContentControl
{
	public static readonly StyledProperty<string> TitleProperty =
		AvaloniaProperty.Register<StatusItem, string>(nameof(Title));

	public static readonly StyledProperty<string> StatusTextProperty =
		AvaloniaProperty.Register<StatusItem, string>(nameof(StatusText));

	public static readonly StyledProperty<object> IconProperty =
		AvaloniaProperty.Register<StatusItem, object>(nameof(Icon));

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public string StatusText
	{
		get => GetValue(StatusTextProperty);
		set => SetValue(StatusTextProperty, value);
	}

	public object Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}
}
