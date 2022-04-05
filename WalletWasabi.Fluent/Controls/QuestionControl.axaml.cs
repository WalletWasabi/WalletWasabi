using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public enum HighlightedButton
{
	None,
	YesButton,
	NoButton
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

	public static readonly StyledProperty<bool> IsYesButtonProperty =
		AvaloniaProperty.Register<QuestionControl, bool>(nameof(IsYesButton));

	public static readonly StyledProperty<bool> IsNoButtonProperty =
		AvaloniaProperty.Register<QuestionControl, bool>(nameof(IsNoButton));

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

	public bool IsYesButton
	{
		get => GetValue(IsYesButtonProperty);
		set => SetValue(IsYesButtonProperty, value);
	}

	public bool IsNoButton
	{
		get => GetValue(IsNoButtonProperty);
		set => SetValue(IsNoButtonProperty, value);
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

	public QuestionControl()
	{
		UpdateHighlightedButton(HighlightButton);
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == HighlightButtonProperty)
		{
			UpdateHighlightedButton(change.NewValue.GetValueOrDefault<HighlightedButton>());
		}
	}

	private void UpdateHighlightedButton(HighlightedButton highlightedButton)
	{
		IsYesButton = highlightedButton == HighlightedButton.YesButton;
		IsNoButton = highlightedButton == HighlightedButton.NoButton;
	}
}
