using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Controls;

public class CurrencyEntryBox : TextBox
{
	public static readonly StyledProperty<string> CurrencyCodeProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, string>(nameof(CurrencyCode));

	public static readonly StyledProperty<bool> IsFiatProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, bool>(nameof(IsFiat));

	public static readonly StyledProperty<bool> IsApproximateProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, bool>(nameof(IsApproximate));

	public static readonly StyledProperty<decimal> ConversionRateProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, decimal>(nameof(ConversionRate));

	private readonly LocalizedInputHelper _defaultInputHelper;
	private readonly LocalizedInputHelper _alternateInputHelper;

	public CurrencyEntryBox()
	{
		_defaultInputHelper = new LocalizedInputHelper('.', ' ');
		_alternateInputHelper = new LocalizedInputHelper(',', ' ');

		Text = string.Empty;

		PseudoClasses.Set(":noexchangerate", true);

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

	private decimal FiatToBitcoin(decimal fiatValue)
	{
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

		var preComposedText = PreComposeText(input);

		var fiatValue = 0m;

		if (_defaultInputHelper.TryParse(preComposedText, out var v1, IsFiat))
		{
			fiatValue = v1;
		}
		else if (_alternateInputHelper.TryParse(preComposedText, out var v2, IsFiat))
		{
			fiatValue = v2;
		}
		else
		{
			e.Handled = true;
		}

		if (IsFiat & !e.Handled)
		{
			e.Handled = FiatToBitcoin(fiatValue) >= Constants.MaximumNumberOfBitcoins;
		}

		base.OnTextInput(e);
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
			// so USD has 14 whole places + the decimal point + 2 decimal places = 17 characters.
			// Bitcoin has "21 000 000 . 0000 0000".
			// Coincidentally the same character count as USD... weird.
			// Plus adding 4 characters for the group separators.
			if (text.Length > 17 + 4)
			{
				text = text[..(17 + 4)];
			}

			if (_defaultInputHelper.ValidateEntryText(text, IsFiat) || _alternateInputHelper.ValidateEntryText(text, IsFiat))
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
	}
}
