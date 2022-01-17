using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public enum HighlightedButton
{
	None,
	YesButton,
	NoButton,
	Both
}

public class QuestionControl : ContentControl
{
	public static readonly StyledProperty<ICommand> YesCommandProperty =
		AvaloniaProperty.Register<QuestionControl, ICommand>(nameof(YesCommand));

	public static readonly StyledProperty<ICommand> NoCommandProperty =
		AvaloniaProperty.Register<QuestionControl, ICommand>(nameof(NoCommand));

	public static readonly StyledProperty<IImage> ImageIconProperty =
		AvaloniaProperty.Register<QuestionControl, IImage>(nameof(ImageIcon));

	public static readonly StyledProperty<object?> IconContentProperty =
		AvaloniaProperty.Register<QuestionControl, object?>(nameof(IconContent));

	public static readonly StyledProperty<HighlightedButton> HighlightButtonProperty =
		AvaloniaProperty.Register<QuestionControl, HighlightedButton>(nameof(HighlightButton));

	public ICommand YesCommand
	{
		get => GetValue(YesCommandProperty);
		set => SetValue(YesCommandProperty, value);
	}

	public ICommand NoCommand
	{
		get => GetValue(NoCommandProperty);
		set => SetValue(NoCommandProperty, value);
	}

	public IImage ImageIcon
	{
		get => GetValue(ImageIconProperty);
		set => SetValue(ImageIconProperty, value);
	}

	public object? IconContent
	{
		get => GetValue(IconContentProperty);
		set => SetValue(IconContentProperty, value);
	}

	public HighlightedButton HighlightButton
	{
		get => GetValue(HighlightButtonProperty);
		set => SetValue(HighlightButtonProperty, value);
	}
}
