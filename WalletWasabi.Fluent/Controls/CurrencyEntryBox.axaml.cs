using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Helpers;
using static WalletWasabi.Userfacing.CurrencyInput;

namespace WalletWasabi.Fluent.Controls;

public partial class CurrencyEntryBox : TextBox
{
	public static readonly StyledProperty<string> CurrencyCodeProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, string>(nameof(CurrencyCode));

	public static readonly StyledProperty<bool> IsFiatProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, bool>(nameof(IsFiat));

	public static readonly StyledProperty<bool> IsApproximateProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, bool>(nameof(IsApproximate));

	public static readonly StyledProperty<decimal> ConversionRateProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, decimal>(nameof(ConversionRate));

	public static readonly StyledProperty<bool> IsRightSideProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, bool>(nameof(IsRightSide));

	public static readonly StyledProperty<int> MaxDecimalsProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, int>(nameof(MaxDecimals), 8);

	public CurrencyEntryBox()
	{
		Text = string.Empty;

		PseudoClasses.Set(":noexchangerate", true);
		PseudoClasses.Set(":isrightside", false);

		this.GetObservable(IsRightSideProperty)
			.Subscribe(x => PseudoClasses.Set(":isrightside", x));

		ModifiedPaste = ReactiveCommand.Create(ModifiedPasteAsync, this.GetObservable(CanPasteProperty));
	}

	public ICommand ModifiedPaste { get; }

	public decimal ConversionRate
	{
		get => GetValue(ConversionRateProperty);
		set => SetValue(ConversionRateProperty, value);
	}

	public string CurrencyCode
	{
		get => GetValue(CurrencyCodeProperty);
		set => SetValue(CurrencyCodeProperty, value);
	}

	public bool IsFiat
	{
		get => GetValue(IsFiatProperty);
		set => SetValue(IsFiatProperty, value);
	}

	public bool IsApproximate
	{
		get => GetValue(IsApproximateProperty);
		set => SetValue(IsApproximateProperty, value);
	}

	public bool IsRightSide
	{
		get => GetValue(IsRightSideProperty);
		set => SetValue(IsRightSideProperty, value);
	}

	public int MaxDecimals
	{
		get => GetValue(MaxDecimalsProperty);
		set => SetValue(MaxDecimalsProperty, value);
	}

	private decimal FiatToBitcoin(decimal fiatValue)
	{
		if (ConversionRate == 0m)
		{
			return 0m;
		}

		return fiatValue / ConversionRate;
	}

	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);

		CaretIndex = Text?.Length ?? 0;

		Dispatcher.UIThread.Post(SelectAll);
	}

	protected override void OnTextInput(TextInputEventArgs e)
	{
		var input = e.Text ?? "";

		// Reject space char input when there's no text.
		if (string.IsNullOrWhiteSpace(Text) && string.IsNullOrWhiteSpace(input))
		{
			e.Handled = true;
			base.OnTextInput(e);
			return;
		}

		if (IsReplacingWithImplicitDecimal(input))
		{
			ReplaceCurrentTextWithLeadingZero(e);

			base.OnTextInput(e);
			return;
		}

		if (IsInsertingImplicitDecimal(input))
		{
			InsertLeadingZeroForDecimal(e);

			base.OnTextInput(e);
			return;
		}

		var preComposedText = PreComposeText(input);

		decimal fiatValue = 0;

		var isValid = ValidateEntryText(preComposedText);

		preComposedText = preComposedText
			.Replace("\r", "")
			.Replace("\n", "")
			.Replace("\t", "")
			.Replace(" ", "");

		var parsed = decimal.TryParse(preComposedText, NumberStyles.Number, InvariantNumberFormat, out fiatValue);

		e.Handled = !(isValid && parsed);

		if (IsFiat && !e.Handled)
		{
			e.Handled = FiatToBitcoin(fiatValue) >= Constants.MaximumNumberOfBitcoins;
		}

		base.OnTextInput(e);
	}

	private bool IsReplacingWithImplicitDecimal(string input)
	{
		return input.StartsWith(".") && SelectedText == Text;
	}

	private bool IsInsertingImplicitDecimal(string input)
	{
		return input.StartsWith(".") && CaretIndex == 0 && Text is not null && !Text.Contains('.');
	}

	private void ReplaceCurrentTextWithLeadingZero(TextInputEventArgs e)
	{
		var finalText = "0" + e.Text;
		Text = "";
		e.Text = finalText;
		CaretIndex = finalText.Length;
		ClearSelection();
	}

	private void InsertLeadingZeroForDecimal(TextInputEventArgs e)
	{
		var prependText = "0" + e.Text;
		Text = Text.Insert(0, prependText);
		e.Text = "";
		CaretIndex += prependText.Length;
	}

	[GeneratedRegex($"^(?<Whole>[0-9{GroupSeparator}]*)(\\{DecimalSeparator}?(?<Frac>[0-9{GroupSeparator}]*))$")]
	private static partial Regex RegexBtcFormat();

	[GeneratedRegex($"^[0-9{GroupSeparator}{DecimalSeparator}]*$")]
	private static partial Regex RegexDecimalCharsOnly();

	[GeneratedRegex($"{GroupSeparator}{{2,}}")]
	private static partial Regex RegexConsecutiveSpaces();

	[GeneratedRegex($"[{GroupSeparator}{DecimalSeparator}]+")]
	private static partial Regex RegexGroupAndDecimal();

	private bool ValidateEntryText(string preComposedText)
	{
		// Check if it has a decimal separator.
		var trailingDecimal = preComposedText.Length > 0 && preComposedText.EndsWith(DecimalSeparator);
		var match = RegexBtcFormat().Match(preComposedText);

		// Ignore group chars on count of the whole part of the decimal.
		var wholeStr = match.Groups["Whole"].ToString();
		var whole = RegexGroupAndDecimal().Replace(wholeStr, "").Length;

		var fracStr = match.Groups["Frac"].ToString().Replace(GroupSeparator, "");
		var frac = RegexGroupAndDecimal().Replace(fracStr, "").Length;

		// Check for consecutive spaces (2 or more) and leading spaces.
		var rule1 = preComposedText.Length > 1 && (preComposedText.StartsWith(GroupSeparator) ||
												   RegexConsecutiveSpaces().IsMatch(preComposedText));

		// Check for trailing spaces in the whole number part and in the last part of the precomp string.
		var rule2 = whole >= 8 && (preComposedText.EndsWith(GroupSeparator) || wholeStr.EndsWith(GroupSeparator));

		// Check for non-numeric chars.
		var rule3 = !RegexDecimalCharsOnly().IsMatch(preComposedText);
		if (rule1 || rule2 || rule3)
		{
			return false;
		}

		// Reject and dont process the input if the string doesnt match.
		if (!match.Success)
		{
			return false;
		}

		// Passthrough the decimal place char or the group separator.
		if (preComposedText == DecimalSeparator && !trailingDecimal)
		{
			return false;
		}

		if (IsFiat)
		{
			// Fiat input restriction is to only allow 2 decimal places max
			// and also 16 whole number places.
			if ((whole > 16 && !trailingDecimal) || frac > 2)
			{
				return false;
			}
		}
		else
		{
			// Bitcoin input restriction is to only allow 8 decimal places max
			// and also 8 whole number places.
			if ((whole > 8 && !trailingDecimal) || frac > MaxDecimals)
			{
				return false;
			}
		}

		return true;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		DoPasteCheck(e);
	}

	private void DoPasteCheck(KeyEventArgs e)
	{
		var keymap = AvaloniaLocator.Current.GetService<PlatformHotkeyConfiguration>();

		bool Match(IEnumerable<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));

		if (keymap is { } && Match(keymap.Paste))
		{
			ModifiedPasteAsync();
		}
		else
		{
			base.OnKeyDown(e);
		}
	}

	public async void ModifiedPasteAsync()
	{
		if (AvaloniaLocator.Current.GetService<IClipboard>() is { } clipboard)
		{
			var text = await clipboard.GetTextAsync();

			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			text = text.Replace("\r", "").Replace("\n", "").Trim();

			// Based on broad M0 money supply figures (80 900 000 000 000.00 USD).
			// so USD has 14 whole places + the decimal point + 2 decimal places = 17 characters
			// plus adding 4 characters for the group separators.
			// Bitcoin has "21 000 000 . 0000 0000".
			// so Bitcoin has 8 whole places + the decimal point + 8 decimal places = 17 characters.
			// plus adding 5 characters for the group separators.
			if (text.Length > 17 + 5)
			{
				text = text[..(17 + 5)];
			}

			if (ValidateEntryText(text))
			{
				OnTextInput(new TextInputEventArgs { Text = text });
			}
		}
	}

	// Pre-composes the TextInputEventArgs to see the potential Text that is to
	// be committed to the TextPresenter in this control.

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

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsReadOnlyProperty)
		{
			PseudoClasses.Set(":readonly", change.NewValue.GetValueOrDefault<bool>());
		}
		else if (change.Property == ConversionRateProperty)
		{
			PseudoClasses.Set(":noexchangerate", change.NewValue.GetValueOrDefault<decimal>() == 0m);
		}
		else if (change.Property == IsFiatProperty)
		{
			PseudoClasses.Set(":isfiat", change.NewValue.GetValueOrDefault<bool>());
		}
	}
}
