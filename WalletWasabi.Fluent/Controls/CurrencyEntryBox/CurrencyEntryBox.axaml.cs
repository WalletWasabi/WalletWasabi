using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Currency;

namespace WalletWasabi.Fluent.Controls;

public partial class CurrencyEntryBox : TextBox
{
	private bool _isUpdating;

	public static readonly StyledProperty<CurrencyFormat> CurrencyFormatProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, CurrencyFormat>(nameof(CurrencyFormat), defaultValue: CurrencyFormat.Btc);

	public static readonly StyledProperty<decimal?> ValueProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, decimal?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

	public static readonly StyledProperty<decimal?> MaxValueProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, decimal?>(nameof(MaxValue));

	public static readonly StyledProperty<string?> ClipboardSuggestionProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, string?>(nameof(ClipboardSuggestion), defaultBindingMode: BindingMode.TwoWay);

	public CurrencyEntryBox()
	{
		Text = "";

		// Set Value and Format Text after Text changes
		// this fires when copying text from clipboard, hitting backspace or delete, etc
		this.GetObservable(TextProperty)
			.Where(x => !_isUpdating)
			.Where(x => CurrencyFormat is { })
			.Do(x =>
			{
				_isUpdating = true;

				// Validate that value can be parsed with current CurrencyFormat
				var value = CurrencyFormat.Parse(x ?? "");
				SetCurrentValue(ValueProperty, value);
				Format();

				_isUpdating = false;
			})
			.Subscribe();

		// Format Text after Value changes
		// this fires when Value is set via Binding e.g: SendViewModel
		this.GetObservable(ValueProperty)
			.Where(x => !_isUpdating)
			.Where(x => CurrencyFormat is { })
			.Do(x => Format())
			.Subscribe();
	}

	public CurrencyFormat CurrencyFormat
	{
		get => GetValue(CurrencyFormatProperty);
		set => SetValue(CurrencyFormatProperty, value);
	}

	public decimal? Value
	{
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public decimal? MaxValue
	{
		get => GetValue(MaxValueProperty);
		set => SetValue(MaxValueProperty, value);
	}

	public string? ClipboardSuggestion
	{
		get => GetValue(ClipboardSuggestionProperty);
		set => SetValue(ClipboardSuggestionProperty, value);
	}

	protected override void OnTextInput(TextInputEventArgs e)
	{
		try
		{
			_isUpdating = true;
			var input = e.Text?.TotalTrim() ?? "";

			// Reject invalid characters
			if (!RegexValidCharacters().Match(input).Success)
			{
				return;
			}

			// Pre-compose Text (Add input to existing Text in the caret position)
			var preComposedText = PreComposeText(input);

			// Reject multiple Decimal Separators
			if (preComposedText.CountOccurrencesOf(CurrencyLocalization.DecimalSeparator) > 1)
			{
				return;
			}

			// Allow appending dot, do not parse
			if (input == CurrencyLocalization.DecimalSeparator && Text is { } && CaretIndex == Text.Length)
			{
				var finalText = Text + CurrencyLocalization.DecimalSeparator;

				// Add trailing zero if it's just the dot
				if (finalText == CurrencyLocalization.DecimalSeparator)
				{
					finalText = "0" + finalText;
				}

				SetCurrentValue(TextProperty, finalText);
				SetCurrentValue(CaretIndexProperty, finalText.Length);
				ClearSelection();
				return;
			}

			// Validate that value can be parsed with current CurrencyFormat
			var value = CurrencyFormat?.Parse(preComposedText);

			// reject input otherwise.
			if (value is null)
			{
				return;
			}

			// Accept input
			base.OnTextInput(e);

			// Set Value for Binding
			SetCurrentValue(ValueProperty, value);

			// Trim Trailing Zeros
			var trimmedZeros = TrimTrailingZeros();
			if (value > 0 && input == "0" && trimmedZeros > 0)
			{
				// and move caret index accordingly
				SetCurrentValue(CaretIndexProperty, CaretIndex - trimmedZeros);
			}

			// Format Text according to current CurrencyFormat
			// Returns the number of separator characters added
			var formattedDifference = Format();
			if (formattedDifference > 0)
			{
				// and move caret index accordingly
				SetCurrentValue(CaretIndexProperty, CaretIndex + formattedDifference);
			}
		}
		finally
		{
			_isUpdating = false;
		}
	}

	[GeneratedRegex($"^[0-9{CurrencyLocalization.DecimalSeparator}]+$")]
	private static partial Regex RegexValidCharacters();

	/// <summary>
	/// Combines newly entered text input and places it in the correct place, accounting for caret index and selected text.
	/// </summary>
	/// <param name="input">the newly entered text input, as received by the OnTextInput() method.</param>
	/// <returns>a string that contains the final text including the newly entered input, replacing any existing selected text.</returns>
	/// <remarks>An event in Avalonia's TextBox with this function should be implemented there for brevity.</remarks>
	private string PreComposeText(string input)
	{
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

	/// <summary>
	/// Trims trailing zeros from Text.
	/// </summary>
	/// <returns>The number of zeros removed.</returns>
	private int TrimTrailingZeros()
	{
		if (Text is null)
		{
			return 0;
		}

		// Trim starting zeros.
		if (Text.StartsWith("0"))
		{
			string corrected;

			// If zeroless starts with a dot, then leave a zero.
			// Else trim all the zeros.
			var zeroless = Text.TrimStart('0');
			if (zeroless.Length == 0)
			{
				corrected = "0";
			}
			else if (zeroless.StartsWith('.'))
			{
				corrected = $"0{Text.TrimStart('0')}";
			}
			else
			{
				corrected = Text.TrimStart('0');
			}

			if (corrected != Text)
			{
				var trimmedLength = Text.Length - corrected.Length;
				SetCurrentValue(TextProperty, corrected);

				return trimmedLength;
			}
		}

		return 0;
	}

	/// <summary>
	/// Formats Text according to CurrencyFormat
	/// </summary>
	/// <returns>The number of group separator characters added.</returns>
	private int Format()
	{
		if (Value is null || Text is null)
		{
			SetCurrentValue(TextProperty, "");
			return 0;
		}

		var formatted = CurrencyFormat.Format(Value.Value);

		if (formatted != Text)
		{
			// Edge case: hitting backspace in "0.1", leaving "0.", in this case we don't want to change the text to "0", because it worsens UX
			if (Text == "0.")
			{
				return 0;
			}

			var difference = formatted.CountOccurrencesOf(CurrencyLocalization.GroupSeparator) - Text.CountOccurrencesOf(CurrencyLocalization.GroupSeparator);
			SetCurrentValue(TextProperty, formatted);
			return difference;
		}

		return 0;
	}
}
