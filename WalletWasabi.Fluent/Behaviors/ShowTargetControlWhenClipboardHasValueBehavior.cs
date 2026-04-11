using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

/// <summary>
/// User clicks a text box and if the clipboard has a value (not whitespace), the target control (e.g. a paste button) will be shown.
/// </summary>
public class ShowTargetControlWhenClipboardHasValueBehavior : Behavior<TextBox>
{
	public static readonly StyledProperty<Control?> TargetControlProperty =
		AvaloniaProperty.Register<ShowTargetControlWhenClipboardHasValueBehavior, Control?>(nameof(TargetControl));

	public Control? TargetControl
	{
		get => GetValue(TargetControlProperty);
		set => SetValue(TargetControlProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		base.OnAttachedToVisualTree();
		AssociatedObject?.GotFocus += OnGotFocusAsync;
	}

	protected override void OnDetachedFromVisualTree()
	{
		base.OnDetachedFromVisualTree();
		AssociatedObject?.GotFocus -= OnGotFocusAsync;
	}

	private async void OnGotFocusAsync(object? sender, GotFocusEventArgs e)
	{
		if (AssociatedObject is null || TargetControl is null)
		{
			return;
		}

		try
		{
			var text = await ApplicationHelper.GetTextAsync();

			// Leading and trailing whitespace is removed for the check purposes.
			TargetControl.IsVisible = !string.IsNullOrEmpty(text?.Trim());
		}
		catch
		{
			TargetControl.IsVisible = false;
		}
	}
}
