using System;
using System.Globalization;
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

		public static readonly StyledProperty<decimal> ConversionProperty =
			AvaloniaProperty.Register<CurrencyEntryBox, decimal>(nameof(Conversion));

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
		private bool _allowConversions = true;
		private bool _allowAmountUpdate = true;
		private readonly CultureInfo _customCultureInfo;
		private readonly char _decimalSeparator = '.';
		private readonly char _groupSeparator = ' ';
		private readonly Regex _matchRegexDecimal;
		private readonly Regex _matchRegexDecimalCharsOnly;

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

			this.GetObservable(TextProperty).Subscribe(_ => DoConversion());
			this.GetObservable(ConversionRateProperty).Subscribe(_ => DoConversion());
			this.GetObservable(ConversionCurrencyCodeProperty).Subscribe(_ => DoConversion());
			this.GetObservable(AmountBtcProperty).Subscribe(x =>
			{
				if (_allowAmountUpdate && x > 0)
				{
					if (IsConversionReversed)
					{
						_allowConversions = false;

						Conversion = BitcoinToFiat(x);
						Text = FormatFiatValue(Conversion);

						_allowConversions = true;
					}
					else
					{
						var formatted = FormatBtcValue(x);

						if (BitcoinInput.TryCorrectAmount(formatted, out var better))
						{
							formatted = better;
						}

						Text = formatted;
					}
				}
			});

			this.GetObservable(ConversionProperty).Subscribe(x =>
			{
				if (IsConversionReversed)
				{
					ConversionText = FullFormatBtc(x);
				}
				else
				{
					ConversionText = FullFormatFiat(x);
				}
			});

			Watermark = "0 BTC";
			Text = string.Empty;

			_matchRegexDecimal =
				new Regex(
					$"^(?<Whole>[0-9{_groupSeparator}]*)(\\{_decimalSeparator}?(?<Frac>[0-9]*))$");

			_matchRegexDecimalCharsOnly =
				new Regex(
					$"^[0-9{_groupSeparator}{_decimalSeparator}]*$");
		}

		private decimal FiatToBitcoin(decimal fiatValue)
		{
			return fiatValue / ConversionRate;
		}

		private decimal BitcoinToFiat(decimal btcValue)
		{
			return btcValue * ConversionRate;
		}

		private void Reverse()
		{
			if (ConversionText != string.Empty)
			{
				_allowConversions = false;

				if (IsConversionReversed)
				{
					if (!string.IsNullOrWhiteSpace(Text))
					{
						Text = FormatBtcValue(Conversion);
					}

					Watermark = "0 BTC";
				}
				else
				{
					if (!string.IsNullOrWhiteSpace(Text))
					{
						Text = FormatFiatValue(Conversion);
					}

					Watermark = $"0.00 {ConversionCurrencyCode}";
				}

				IsConversionReversed = !IsConversionReversed;

				_allowConversions = true;

				DoConversion();

				CaretIndex = Text.Length;
			}
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			base.OnGotFocus(e);

			CaretIndex = Text?.Length ?? 0;

			Dispatcher.UIThread.Post(() => SelectAll());
		}

		protected override void OnTextInput(TextInputEventArgs e)
		{
			if (_allowConversions)
			{
				var inputText = e.Text ?? "";
				var inputLength = inputText.Length;

				// Check if it has a decimal separator.
				var trailingDecimal = inputLength > 0 && inputText[^1] == _decimalSeparator;
				var preComposedText = PreComposeText(e);

				if (!_matchRegexDecimalCharsOnly.IsMatch(preComposedText))
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

		private string FormatBtcValue(decimal value)
		{
			return string.Format(_customCultureInfo.NumberFormat, "{0:### ### ### ##0.########}", value).Trim();
		}

		private string FormatFiatValue(decimal value)
		{
			return string.Format(_customCultureInfo.NumberFormat, "{0:N2}", value).Trim();
		}

		private string FullFormatBtc(decimal value)
		{
			return $"{FormatBtcValue(value)} BTC";
		}

		private string FullFormatFiat(decimal value)
		{
			return $"≈ {FormatFiatValue(Conversion)}" + (!string.IsNullOrWhiteSpace(ConversionCurrencyCode)
				? $" {ConversionCurrencyCode}"
				: "");
		}

		private void DoConversion()
		{
			if (_allowConversions)
			{
				if (IsConversionReversed)
				{
					if (decimal.TryParse(Text, NumberStyles.Number, _customCultureInfo, out var result) && ConversionRate > 0)
					{
						CurrencyCode = ConversionCurrencyCode;

						Conversion = FiatToBitcoin(result);

						_allowAmountUpdate = false;
						AmountBtc = Conversion;
						_allowAmountUpdate = true;
					}
					else
					{
						AmountBtc = 0;
						Conversion = 0;
						CurrencyCode = ConversionCurrencyCode;
					}
				}
				else
				{
					if (decimal.TryParse(Text, NumberStyles.Number, _customCultureInfo, out var result) && ConversionRate > 0)
					{
						CurrencyCode = "BTC";

						Conversion = BitcoinToFiat(result);

						_allowAmountUpdate = false;
						AmountBtc = result;
						_allowAmountUpdate = true;
					}
					else
					{
						AmountBtc = 0;
						Conversion = 0;
						CurrencyCode = "BTC";
					}
				}
			}
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
			Reverse();
		}

		public decimal AmountBtc
		{
			get => GetValue(AmountBtcProperty);
			set => SetValue(AmountBtcProperty, value);
		}

		public decimal Conversion
		{
			get => GetValue(ConversionProperty);
			set => SetValue(ConversionProperty, value);
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
