using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls;

public class IntTextBox : TextBox
{
	protected override Type StyleKeyOverride => typeof(TextBox);

	private static readonly string[] InvalidCharacters = ["\u007f"];

	public static readonly StyledProperty<int> MaximumProperty =
		AvaloniaProperty.Register<NumericUpDown, int>(nameof(Maximum), int.MaxValue);

	public static readonly StyledProperty<int> MinimumProperty =
		AvaloniaProperty.Register<NumericUpDown, int>(nameof(Minimum), int.MinValue);

	public int Maximum
	{
		get => GetValue(MaximumProperty);
		set => SetValue(MaximumProperty, value);
	}

	public int Minimum
	{
		get => GetValue(MinimumProperty);
		set => SetValue(MinimumProperty, value);
	}

	public async void ModifiedPasteAsync()
	{
		var text = await ApplicationHelper.GetTextAsync();

		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		if (IsInvalidInt(text))
		{
			return;
		}

		OnTextInput(new TextInputEventArgs { Text = text });

		Dispatcher.UIThread.Post(() =>
		{
			ClearSelection();
			CaretIndex = Text?.Length ?? 0;
		});
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		DoPasteCheck(e);
	}

	protected override void OnTextInput(TextInputEventArgs e)
	{
		var input = e.Text == null ? "" : e.Text.TotalTrim();

		if (string.IsNullOrWhiteSpace(Text) && string.IsNullOrWhiteSpace(input))
		{
			e.Handled = true;
			base.OnTextInput(e);
			return;
		}

		var preComposedText = PreComposeText(input);

		if (IsInvalidInt(preComposedText))
		{
			var result = InsertDefaultValue();
			base.OnTextInput(result);
			return;
		}

		base.OnTextInput(e);
	}

	private void DoPasteCheck(KeyEventArgs e)
	{
		var keymap = Application.Current?.PlatformSettings?.HotkeyConfiguration;

		bool Match(IEnumerable<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));

		if (keymap is { } && Match(keymap.Paste))
		{
			ModifiedPasteAsync();
			e.Handled = true;
		}
		else
		{
			base.OnKeyDown(e);
		}
	}

	private bool IsInvalidInt(string input)
	{
		if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var intValue))
		{
			return intValue < Minimum || intValue > Maximum;
		}

		return true;
	}

	private TextInputEventArgs InsertDefaultValue()
	{
		var finalText = "";
		SetCurrentValue(TextProperty, "");
		SetCurrentValue(CaretIndexProperty, finalText.Length);
		ClearSelection();
		return new TextInputEventArgs { Text = finalText };
	}

	private string RemoveInvalidCharacters(string text)
	{
		for (var i = 0; i < InvalidCharacters.Length; i++)
		{
			text = text.Replace(InvalidCharacters[i], string.Empty);
		}

		return text;
	}

	// An event in Avalonia's TextBox with this function should be implemented there for brevity.
	private string PreComposeText(string input)
	{
		input = RemoveInvalidCharacters(input);
		var preComposedText = Text ?? "";
		var caretIndex = CaretIndex;
		var selectionStart = SelectionStart;
		var selectionEnd = SelectionEnd;

		if (!string.IsNullOrEmpty(input) && (MaxLength == 0 ||
											 input.Length + preComposedText.Length -
											 Math.Abs(selectionStart - selectionEnd) <= MaxLength))
		{
			if (selectionStart != selectionEnd)
			{
				var start = Math.Min(selectionStart, selectionEnd);
				var end = Math.Max(selectionStart, selectionEnd);
				preComposedText = $"{preComposedText[..start]}{preComposedText[end..]}";
				caretIndex = start;
			}

			return $"{preComposedText[..caretIndex]}{input}{preComposedText[caretIndex..]}";
		}

		return "";
	}
}
