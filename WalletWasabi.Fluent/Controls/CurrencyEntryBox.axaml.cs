using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Controls
{
	public class CurrencyEntryBox : TextBox
	{
		public static readonly DirectProperty<CurrencyEntryBox, decimal> AmountBtcProperty =
			AvaloniaProperty.RegisterDirect<CurrencyEntryBox, decimal>(
				nameof(AmountBtc),
				o => o.AmountBtc,
				(o, v) => o.AmountBtc = v,
				enableDataValidation: true,
				defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<string> ConversionTextProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, string>(nameof(ConversionText));

		public static readonly StyledProperty<decimal> ConversionRateProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, decimal>(nameof(ConversionRate));

		public static readonly StyledProperty<string> CurrencyCodeProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, string>(nameof(CurrencyCode));

		public static readonly StyledProperty<string> ConversionCurrencyCodeProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, string>(nameof(ConversionCurrencyCode));

		public static readonly StyledProperty<bool> IsConversionReversedProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, bool>(nameof(IsConversionReversed));

		private readonly CultureInfo _customCultureInfo;
		private readonly char _decimalSeparator = '.';
		private readonly char _groupSeparator = ' ';
		private readonly Regex _regexBTCFormat;
		private readonly Regex _regexDecimalCharsOnly;
		private readonly Regex _regexConsecutiveSpaces;
		private readonly Regex _regexGroupAndDecimal;
		private Button? _swapButton;
		private CompositeDisposable? _disposable;
		private bool _canUpdateDisplay = true;
		private decimal _amountBtc;

		public CurrencyEntryBox()
		{
			_customCultureInfo = new CultureInfo("")
			{
				NumberFormat =
				{
					CurrencyGroupSeparator = _groupSeparator.ToString(),
					NumberGroupSeparator = _groupSeparator.ToString(),
					CurrencyDecimalSeparator = _decimalSeparator.ToString(),
					NumberDecimalSeparator = _decimalSeparator.ToString()
				}
			};

			this.GetObservable(TextProperty).Subscribe(InputText);
			this.GetObservable(ConversionRateProperty).Subscribe(_ => UpdateDisplay(false));
			this.GetObservable(ConversionCurrencyCodeProperty).Subscribe(_ => UpdateDisplay(true));
			this.GetObservable(AmountBtcProperty).Subscribe(_ => UpdateDisplay(true));
			this.GetObservable(IsReadOnlyProperty).Subscribe(_ => UpdateDisplay(true));

			Watermark = "0 BTC";
			Text = string.Empty;

			_regexBTCFormat =
				new Regex(
					$"^(?<Whole>[0-9{_groupSeparator}]*)(\\{_decimalSeparator}?(?<Frac>[0-9{_groupSeparator}]*))$",
					RegexOptions.Compiled);

			_regexDecimalCharsOnly =
				new Regex(
					$"^[0-9{_groupSeparator}{_decimalSeparator}]*$", RegexOptions.Compiled);

			_regexConsecutiveSpaces =
				new Regex(
					$"{_groupSeparator}{{2,}}", RegexOptions.Compiled);

			_regexGroupAndDecimal =
				new Regex(
					$"[{_groupSeparator}{_decimalSeparator}]+", RegexOptions.Compiled);

			PseudoClasses.Set(":noexchangerate", true);
		}

		public decimal AmountBtc
		{
			get => _amountBtc;
			set => SetAndRaise(AmountBtcProperty, ref _amountBtc, value);
		}

		public string ConversionText
		{
			get => GetValue(ConversionTextProperty);
			set => SetValue(ConversionTextProperty, value);
		}

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

		public string ConversionCurrencyCode
		{
			get => GetValue(ConversionCurrencyCodeProperty);
			set => SetValue(ConversionCurrencyCodeProperty, value);
		}

		public bool IsConversionReversed
		{
			get => GetValue(IsConversionReversedProperty);
			set => SetValue(IsConversionReversedProperty, value);
		}

		private decimal FiatToBitcoin(decimal fiatValue)
		{
			return fiatValue / ConversionRate;
		}

		private decimal BitcoinToFiat(decimal btcValue)
		{
			return btcValue * ConversionRate;
		}

		protected override void UpdateDataValidation<T>(AvaloniaProperty<T> property, BindingValue<T> value)
		{
			if (property == AmountBtcProperty)
			{
				DataValidationErrors.SetError(this, value.Error);
			}
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			base.OnGotFocus(e);

			CaretIndex = Text?.Length ?? 0;

			Dispatcher.UIThread.Post(SelectAll);
		}

		protected override void OnLostFocus(RoutedEventArgs e)
		{
			base.OnLostFocus(e);

			UpdateDisplay(true);
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

			var preComposedText = PreComposeText(e.Text);

			decimal fiatValue = 0;

			e.Handled = !(ValidateEntryText(preComposedText) &&
						decimal.TryParse(preComposedText.Replace($"{_groupSeparator}", ""), NumberStyles.Number, _customCultureInfo, out fiatValue));

			if (IsConversionReversed & !e.Handled)
			{
				e.Handled = FiatToBitcoin(fiatValue) >= Constants.MaximumNumberOfBitcoins;
			}

			base.OnTextInput(e);
		}

		private bool ValidateEntryText(string preComposedText)
		{
			// Check if it has a decimal separator.
			var trailingDecimal = preComposedText.Length > 0 && preComposedText[^1] == _decimalSeparator;
			var match = _regexBTCFormat.Match(preComposedText);

			// Ignore group chars on count of the whole part of the decimal.
			var wholeStr = match.Groups["Whole"].ToString();
			var whole = _regexGroupAndDecimal.Replace(wholeStr, "").Length;

			var fracStr = match.Groups["Frac"].ToString().Replace($"{_groupSeparator}", "");
			var frac = _regexGroupAndDecimal.Replace(fracStr, "").Length;

			// Check for consecutive spaces (2 or more) and leading spaces.
			var rule1 = preComposedText.Length > 1 && (preComposedText[0] == _groupSeparator ||
													   _regexConsecutiveSpaces.IsMatch(preComposedText));

			// Check for trailing spaces in the whole number part and in the last part of the precomp string.
			var rule2 = whole >= 8 && (preComposedText.Last() == _groupSeparator || wholeStr.Last() == _groupSeparator);

			// Check for non-numeric chars.
			var rule3 = !_regexDecimalCharsOnly.IsMatch(preComposedText);
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
			switch (preComposedText.Length)
			{
				case 1 when preComposedText[0] == _decimalSeparator && !trailingDecimal:
					return false;
			}

			if (IsConversionReversed)
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
				if ((whole > 8 && !trailingDecimal) || frac > 8)
				{
					return false;
				}
			}

			return true;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			DoPasteCheckAsync(e);
		}

		private async void DoPasteCheckAsync(KeyEventArgs e)
		{
			var keymap = AvaloniaLocator.Current.GetService<PlatformHotkeyConfiguration>();

			bool Match(List<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));

			if (Match(keymap.Paste))
			{
				ModifiedPaste();
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		public async void ModifiedPaste()
		{
			var text = await AvaloniaLocator.Current.GetService<IClipboard>().GetTextAsync();

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
				text = text.Substring(0, 17 + 4);
			}

			if (ValidateEntryText(text))
			{
				OnTextInput(new TextInputEventArgs { Text = text });
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
					preComposedText = preComposedText.Substring(0, start) + preComposedText.Substring(end);
					caretIndex = start;
				}

				return preComposedText.Substring(0, caretIndex) + input + preComposedText.Substring(caretIndex);
			}

			return "";
		}

		private static string FullFormatBtc(NumberFormatInfo formatInfo, decimal value)
		{
			return $"{value.FormattedBtc()} BTC";
		}

		private static string FullFormatFiat(
			NumberFormatInfo formatInfo,
			decimal value,
			string currencyCode,
			bool approximate)
		{
			return (approximate ? "≈ " : "") + $"{value.FormattedFiat()}" +
				   (!string.IsNullOrWhiteSpace(currencyCode)
					   ? $" {currencyCode}"
					   : "");
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_disposable?.Dispose();
			_disposable = new CompositeDisposable();

			_swapButton = e.NameScope.Find<Button>("PART_SwapButton");

			if (_swapButton is { })
			{
				_swapButton.Click += SwapButtonOnClick;

				_disposable.Add(Disposable.Create(() => _swapButton.Click -= SwapButtonOnClick));
			}

			UpdateDisplay(true);
		}

		private void SwapButtonOnClick(object? sender, RoutedEventArgs e)
		{
			IsConversionReversed = !IsConversionReversed;
			UpdateDisplay(true);
			ClearSelection();
		}

		private void InputText(string text)
		{
			if (!_canUpdateDisplay)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(text))
			{
				InputBtcValue(0);
				UpdateDisplay(false);
			}
			else
			{
				if (IsConversionReversed)
				{
					InputFiatString(text);
				}
				else
				{
					InputBtcString(text);
				}
			}
		}

		private void InputFiatString(string value)
		{
			if (decimal.TryParse(value, NumberStyles.Number, _customCultureInfo, out var decimalValue))
			{
				InputBtcValue(FiatToBitcoin(decimalValue));
			}

			UpdateDisplay(false);
		}

		private void InputBtcString(string value)
		{
			if (BitcoinInput.TryCorrectAmount(value, out var better))
			{
				if (better != Constants.MaximumNumberOfBitcoins.ToString())
				{
					value = better;
				}
			}

			if (decimal.TryParse(value, NumberStyles.Number, _customCultureInfo, out var decimalValue))
			{
				InputBtcValue(decimalValue);
			}

			UpdateDisplay(false);
		}

		private void InputBtcValue(decimal value)
		{
			AmountBtc = value;
		}

		private void UpdateDisplay(bool updateTextField)
		{
			if (ConversionRate == 0m)
			{
				return;
			}

			var conversion = BitcoinToFiat(AmountBtc);

			if (IsConversionReversed && !IsReadOnly)
			{
				CurrencyCode = ConversionCurrencyCode;
				ConversionText = FullFormatBtc(_customCultureInfo.NumberFormat, AmountBtc);
				Watermark = FullFormatFiat(_customCultureInfo.NumberFormat, 0, ConversionCurrencyCode, false);

				if (updateTextField)
				{
					_canUpdateDisplay = false;
					Text = AmountBtc > 0 ? conversion.FormattedFiat() : string.Empty;
					_canUpdateDisplay = true;
				}
			}
			else
			{
				CurrencyCode = "BTC";

				ConversionText = FullFormatFiat(
					_customCultureInfo.NumberFormat,
					conversion,
					ConversionCurrencyCode,
					true);

				Watermark = FullFormatBtc(_customCultureInfo.NumberFormat, 0);

				if (updateTextField)
				{
					_canUpdateDisplay = false;
					Text = AmountBtc > 0 ? AmountBtc.FormattedBtc() : string.Empty;
					_canUpdateDisplay = true;
				}
			}
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
}