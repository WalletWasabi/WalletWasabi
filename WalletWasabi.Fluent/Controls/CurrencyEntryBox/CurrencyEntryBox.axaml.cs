using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Userfacing;

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

	private string _currentText;

	public CurrencyEntryBox()
	{
		Text = "";

		// Set Value and Format Text after Text changes
		// this fires when copying text from clipboard, hitting backspace or delete, etc
		this.GetObservable(TextProperty)
			.Where(_ => !_isUpdating)
			.Do(x =>
			{
				_isUpdating = true;

				// Validate that value can be parsed with current CurrencyFormat
				var result = CurrencyFormat.Parse(x ?? "");

				decimal? value =
					result switch
					{
						CurrencyFormatParseResult.Nan => null,
						CurrencyFormatParseResult.OutOfRange => null,
						CurrencyFormatParseResult.Ok ok => ok.Value
					};

				SetCurrentValue(ValueProperty, value);

				if (result is CurrencyFormatParseResult.Ok)
				{
					var formattedDifference = Format(x);
					if (formattedDifference != 0 && CaretIndex < Text.Length)
					{
						Console.WriteLine($"Moving Caret Index: {CaretIndex} => {CaretIndex + formattedDifference}");
						// and move caret index accordingly
						Dispatcher.UIThread.InvokeAsync(() => SetCurrentValue(CaretIndexProperty, CaretIndex + formattedDifference));
					}
				}

				_currentText = Text;

				_isUpdating = false;
			})
			.Skip(1)
			.Subscribe();

		// Format Text after Value changes
		// this fires when Value is set via Binding e.g: SendViewModel
		this.GetObservable(ValueProperty)
			.Where(_ => !_isUpdating)
			.Do(_ => OnValueUpdated())
			.Subscribe();

		// Handle copying full text to the clipboard
		Observable.FromEventPattern<RoutedEventArgs>(this, nameof(CopyingToClipboard))
			  .Select(x => x.EventArgs)
			  .Where(_ => Value is { })
			  .Where(_ => SelectedText == Text)
			  .DoAsync(OnCopyingFullTextToClipboardAsync)
			  .Subscribe();

		// Handle pasting full text from clipboard
		Observable.FromEventPattern<RoutedEventArgs>(this, nameof(PastingFromClipboard))
			.Select(x => x.EventArgs)
			.Where(_ => string.IsNullOrWhiteSpace(Text) || SelectedText == Text)
			.Throttle(TimeSpan.FromMilliseconds(50))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(_ => SelectAll())
			.Subscribe();

		// Set MaxLength according to CurrencyFormat
		this.GetObservable(CurrencyFormatProperty)
			.WhereNotNull()
			.Select(x => x.MaxLength)
			.WhereNotNull()
			.Do(maxLength => SetCurrentValue(MaxLengthProperty, maxLength))
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

	protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
	{
		if (property == ValueProperty)
		{
			DataValidationErrors.SetError(this, error);
		}
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
			if (input == CurrencyInput.DecimalSeparator && Text is { } && Text.Contains(CurrencyInput.DecimalSeparator))
			{
				// Unless replacing the whole text
				if (input != preComposedText)
				{
					return;
				}
			}

			// Automatically add integral zero when typing only "."
			if (preComposedText == CurrencyInput.DecimalSeparator)
			{
				preComposedText = "0" + CurrencyInput.DecimalSeparator;
				base.OnTextInput(new TextInputEventArgs { Text = preComposedText });
				return;
			}

			// Validate that value can be parsed with current CurrencyFormat
			var result = CurrencyFormat.Parse(preComposedText);

			// reject input otherwise.
			if (result is not CurrencyFormatParseResult.Ok ok)
			{
				return;
			}

			// Accept input
			base.OnTextInput(e);

			decimal? value = ok.Value;

			// Set Value for Binding
			SetCurrentValue(ValueProperty, value);

			if (result is not CurrencyFormatParseResult.Ok)
			{
				return;
			}

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

			_currentText = preComposedText;
		}
		finally
		{
			_isUpdating = false;
		}
	}

	[GeneratedRegex($"^[0-9{CurrencyInput.DecimalSeparator}]+$")]
	private static partial Regex RegexValidCharacters();

	/// <summary>
	/// Combines newly entered text input and places it in the correct place, accounting for caret index and selected text.
	/// </summary>
	/// <param name="input">The newly entered text input, as received by the OnTextInput() method.</param>
	/// <returns>A string that contains the final text including the newly entered input, replacing any existing selected text.</returns>
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
	private int Format(string? oldValue = null)
	{
		if (Value is null || Text is null)
		{
			SetCurrentValue(TextProperty, "");
			return 0;
		}

		var currentText = oldValue ?? Text;

		var formatted = CurrencyFormat.Format(Value.Value);

		if (formatted != currentText)
		{
			var difference = formatted.CountOccurrencesOf(CurrencyInput.GroupSeparator) - currentText.CountOccurrencesOf(CurrencyInput.GroupSeparator);

			// Edge case: hitting backspace in for example "45.1", leaving "45.", in this case we don't want to change the text to "45", because it worsens UX
			if (!currentText.EndsWith(CurrencyInput.DecimalSeparator))
			{
				SetCurrentValue(TextProperty, formatted);
			}

			return difference;
		}

		return 0;
	}

	/// <summary>
	/// Specialized copy to clipboard that copies the Value, formatted according to localization rules
	/// </summary>
	private async Task OnCopyingFullTextToClipboardAsync(RoutedEventArgs e)
	{
		if (ApplicationHelper.Clipboard is not { } clipboard || Value is not { } value)
		{
			return;
		}

		await clipboard.SetTextAsync(value.ToString(CurrencyInput.InvariantNumberFormat));

		e.Handled = true;
	}

	/// <summary>
	/// Fired when the Value property is updated by means other than user input (e.g Databinding)
	/// </summary>
	private void OnValueUpdated()
	{
		if (Text is null)
		{
			return;
		}

		Format();
		SetCurrentValue(CaretIndexProperty, Text.Length);
		if (!string.IsNullOrWhiteSpace(SelectedText))
		{
			SelectAll();
		}
	}
}
