using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Controls
{
	public class CurrencyEntryBox : TextBox
	{
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
		private CompositeDisposable _disposable;
		private bool _allowConversions = true;
		private NumberFormatInfo _cultureNumberFormatInfo;
		private char _currentCultureDecimalSeparator;
		private char _currentCultureGroupSeparator;
		private Regex _matchRegexDecimal;
		private Regex _matchRegexDecimalCharsOnly;

		public CurrencyEntryBox()
		{
			this.GetObservable(TextProperty).Subscribe(_ => DoConversion());
			this.GetObservable(ConversionRateProperty).Subscribe(_ => DoConversion());
			Text = "0";

			_cultureNumberFormatInfo = CultureInfo.CurrentCulture.NumberFormat;
			_cultureNumberFormatInfo.CurrencyGroupSeparator = " ";
			_cultureNumberFormatInfo.NumberGroupSeparator = " ";
			_cultureNumberFormatInfo.CurrencyDecimalSeparator = ".";
			_cultureNumberFormatInfo.NumberDecimalSeparator = ".";

			_currentCultureDecimalSeparator = Convert.ToChar(_cultureNumberFormatInfo.NumberDecimalSeparator);
			_currentCultureGroupSeparator = Convert.ToChar(_cultureNumberFormatInfo.NumberGroupSeparator);
			_matchRegexDecimal =
				new Regex(
					$"^(?<Whole>[0-9{_currentCultureGroupSeparator}]*)(\\{_currentCultureDecimalSeparator}?(?<Frac>[0-9]*))$");

			_matchRegexDecimalCharsOnly =
				new Regex(
					$"^[0-9{_currentCultureGroupSeparator}{_currentCultureDecimalSeparator}]*$");
		}

		private void Reverse()
		{
			if (ConversionText != string.Empty)
			{
				_allowConversions = false;

				if (IsConversionReversed)
				{
					Text = $"{FormatBtc(Conversion)}";
				}
				else
				{
					Text = $"{FormatFiat(Conversion)}";
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

			CaretIndex = Text.Length;

			Dispatcher.UIThread.Post(() => SelectAll());
		}

		protected override void OnTextInput(TextInputEventArgs e)
		{
			if (_allowConversions)
			{
				var inputText = e.Text ?? "";
				var inputLength = inputText.Length;

				// Check if it has a decimal separator.
				var trailingDecimal = inputLength > 0 && inputText[^1] == _currentCultureDecimalSeparator;
				var precompText = PrecomposeText(e) ?? "";

				if (!_matchRegexDecimalCharsOnly.IsMatch(precompText))
				{
					e.Handled = true;
					base.OnTextInput(e);
					return;
				}

				var match = _matchRegexDecimal.Match(precompText);

				// Ignore group chars on count of the whole part of the decimal.
				var wholeStr = match.Groups["Whole"].ToString();
				var whole = wholeStr
					.Replace(_currentCultureGroupSeparator, char.MinValue)
					.Replace(_currentCultureDecimalSeparator, char.MinValue)
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
					case 1 when inputText[0] == _currentCultureDecimalSeparator && !trailingDecimal:
					case 1 when inputText[0] == _currentCultureGroupSeparator && !fracStr.Contains(_currentCultureGroupSeparator):
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

		// Precomposes the TextInputEventArgs to see the potential Text that is to
		// be commited to the TextPresenter in this control.

		// An event in Avalonia's TextBox with this function should be implemented there for brevity.
		private string PrecomposeText(TextInputEventArgs e)
		{
			var input = e.Text;

			input = RemoveInvalidCharacters(input);
			var precomposedText = Text ?? "";
			var caretIndex = CaretIndex;
			var selectionStart = SelectionStart;
			var selectionEnd = SelectionEnd;

			if (!string.IsNullOrEmpty(input) && (MaxLength == 0 ||
			                                     input.Length + precomposedText.Length -
			                                     Math.Abs(selectionStart - selectionEnd) <= MaxLength))
			{

				if (selectionStart != selectionEnd)
				{
					var start = Math.Min(selectionStart, selectionEnd);
					var end = Math.Max(selectionStart, selectionEnd);
					precomposedText = precomposedText.Substring(0, start) + precomposedText.Substring(end);
					caretIndex = start;
				}
				return precomposedText.Substring(0, caretIndex) + input + precomposedText.Substring(caretIndex);
			}
			return "";
		}

		private string FormatBtc(decimal value)
		{
			return string.Format(_cultureNumberFormatInfo, "{0:0.########}", value);
		}

		private string FormatFiat(decimal value)
		{
			return string.Format(_cultureNumberFormatInfo, "{0:N2}", value);
		}

		private void DoConversion()
		{
			if (_allowConversions)
			{
				if (IsConversionReversed)
				{
					if (decimal.TryParse(Text, out var result) && ConversionRate > 0)
					{
						CurrencyCode = ConversionCurrencyCode;

						Conversion = result / ConversionRate;

						ConversionText = $"≈ {FormatBtc(Conversion)} BTC";
					}
					else
					{
						Conversion = 0;
						ConversionText = string.Empty;
						CurrencyCode = "";
					}
				}
				else
				{
					if (decimal.TryParse(Text, out var result) && ConversionRate > 0)
					{
						CurrencyCode = "BTC";

						Conversion = result * ConversionRate;

						ConversionText = $"≈ {FormatFiat(Conversion)}" + (!string.IsNullOrWhiteSpace(ConversionCurrencyCode)
							? $" {ConversionCurrencyCode}"
							: "");
					}
					else
					{
						Conversion = 0;
						ConversionText = string.Empty;
						CurrencyCode = "";
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