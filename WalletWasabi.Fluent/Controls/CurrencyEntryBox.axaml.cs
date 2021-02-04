using System;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Controls
{
	public class CurrencyEntryBox : TextBox
	{
		public static readonly StyledProperty<decimal> AmountBtcProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, decimal>(nameof(AmountBtc), defaultBindingMode: BindingMode.TwoWay);

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

		private Button? _swapButton;
		private CompositeDisposable? _disposable;
		private readonly CultureInfo _customCultureInfo;
		private readonly char _decimalSeparator = '.';
		private readonly char _groupSeparator = ' ';
		private readonly Regex _matchRegexDecimal;
		private readonly Regex _matchRegexDecimalCharsOnly;
		private bool _canUpdateDisplay = true;
		private Regex _matchRegexConsecutiveSpaces;

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

			Watermark = "0 BTC";
			Text = string.Empty;

			_matchRegexDecimal =
				new Regex(
					$"^(?<Whole>[0-9{_groupSeparator}]*)(\\{_decimalSeparator}?(?<Frac>[0-9]*))$");

			_matchRegexDecimalCharsOnly =
				new Regex(
					$"^[0-9{_groupSeparator}{_decimalSeparator}]*$");

			_matchRegexConsecutiveSpaces =
				new Regex(
					$"{_groupSeparator}{{2,}}");
		}

		private decimal FiatToBitcoin(decimal fiatValue)
		{
			return fiatValue / ConversionRate;
		}

		private decimal BitcoinToFiat(decimal btcValue)
		{
			return btcValue * ConversionRate;
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			base.OnGotFocus(e);

			CaretIndex = Text?.Length ?? 0;

			Dispatcher.UIThread.Post(SelectAll);
		}

		protected override void OnTextInput(TextInputEventArgs e)
		{
			var inputText = e.Text ?? "";
			var inputLength = inputText.Length;

			// Check if it has a decimal separator.
			var trailingDecimal = inputLength > 0 && inputText[^1] == _decimalSeparator;
			var preComposedText = PreComposeText(e);

			if ((preComposedText.Length > 1 && preComposedText[0] == _groupSeparator
			     || preComposedText.Last() == _groupSeparator
			     || _matchRegexConsecutiveSpaces.IsMatch(preComposedText))
			    || !_matchRegexDecimalCharsOnly.IsMatch(preComposedText))
			{
				e.Handled = true;
				base.OnTextInput(e);
				return;
			}

			var match = _matchRegexDecimal.Match(preComposedText);

			// Ignore group chars on count of the whole part of the decimal.
			var wholeStr = match.Groups["Whole"].ToString();
			var whole = wholeStr
				.Replace(_groupSeparator, char.MinValue)
				.Replace(_decimalSeparator, char.MinValue)
				.Length;

			var fracStr = match.Groups["Frac"].ToString();
			var frac = fracStr.Length;

			// Reject and dont process the input if the string doesnt match.
			if (!match.Success)
			{
				e.Handled = true;
				base.OnTextInput(e);
				return;
			}

			// Passthrough the decimal place char or the group separator.
			switch (inputLength)
			{
				case 1 when inputText[0] == _decimalSeparator && !trailingDecimal:
				case 1 when inputText[0] == _groupSeparator && !fracStr.Contains(_groupSeparator):
					base.OnTextInput(e);
					return;
			}

			if (IsConversionReversed)
			{
				// Fiat input restriction is to only allow 2 decimal places.
				if (frac > 2)
				{
					e.Handled = true;
				}
			}
			else
			{
				// Bitcoin input restriction is to only allow 8 decimal places max
				// and also 8 whole number places.
				if (whole > 8 && !trailingDecimal || frac > 8)
				{
					e.Handled = true;
				}
			}

			base.OnTextInput(e);
		}

		// Pre-composes the TextInputEventArgs to see the potential Text that is to
		// be committed to the TextPresenter in this control.

		// An event in Avalonia's TextBox with this function should be implemented there for brevity.
		private string PreComposeText(TextInputEventArgs e)
		{
			var input = e.Text;

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

		private static string FormatBtcValue(NumberFormatInfo formatInfo, decimal value)
		{
			return string.Format(formatInfo, "{0:### ### ### ##0.########}", value).Trim();
		}

		private static string FormatFiatValue(NumberFormatInfo formatInfo, decimal value)
		{
			return string.Format(formatInfo, "{0:N2}", value).Trim();
		}

		private static string FullFormatBtc(NumberFormatInfo formatInfo, decimal value)
		{
			return $"{FormatBtcValue(formatInfo, value)} BTC";
		}

		private static string FullFormatFiat(NumberFormatInfo formatInfo, decimal value, string currencyCode, bool approximate)
		{
			return (approximate ? "≈ " : "") + $"{FormatFiatValue(formatInfo, value)}" + (!string.IsNullOrWhiteSpace(currencyCode)
				? $" {currencyCode}"
				: "");
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_disposable?.Dispose();
			_disposable = new CompositeDisposable();

			_swapButton = e.NameScope.Find<Button>("PART_SwapButton");

			_swapButton.Click += SwapButtonOnClick;

			_disposable.Add(Disposable.Create(() => _swapButton.Click -= SwapButtonOnClick));
		}

		private void SwapButtonOnClick(object? sender, RoutedEventArgs e)
		{
			IsConversionReversed = !IsConversionReversed;
			UpdateDisplay(true);
			ClearSelection();
		}

		private void InputText(string text)
		{
			if(!_canUpdateDisplay)
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
				value = better;
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
			var conversion = BitcoinToFiat(AmountBtc);

			if (IsConversionReversed)
			{
				CurrencyCode = ConversionCurrencyCode;
				ConversionText = FullFormatBtc(_customCultureInfo.NumberFormat, AmountBtc);
				Watermark = FullFormatFiat(_customCultureInfo.NumberFormat, 0, ConversionCurrencyCode, false);

				if (updateTextField)
				{
					_canUpdateDisplay = false;
					Text = AmountBtc > 0 ? FormatFiatValue(_customCultureInfo.NumberFormat, conversion) : string.Empty;
					_canUpdateDisplay = true;
				}
			}
			else
			{
				CurrencyCode = "BTC";
				ConversionText = FullFormatFiat(_customCultureInfo.NumberFormat, conversion, ConversionCurrencyCode, true);
				Watermark = FullFormatBtc(_customCultureInfo.NumberFormat, 0);

				if (updateTextField)
				{
					_canUpdateDisplay = false;
					Text = AmountBtc > 0 ? FormatBtcValue(_customCultureInfo.NumberFormat, AmountBtc) : string.Empty;
					_canUpdateDisplay = true;
				}
			}
		}

		public decimal AmountBtc
		{
			get => GetValue(AmountBtcProperty);
			set => SetValue(AmountBtcProperty, value);
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
	}
}
