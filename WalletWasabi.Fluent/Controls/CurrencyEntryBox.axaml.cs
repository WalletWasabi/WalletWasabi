using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

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

		public CurrencyEntryBox()
		{
			this.GetObservable(TextProperty).Subscribe(_ => DoConversion());
		}

		private void Reverse()
		{
			if (ConversionText != string.Empty)
			{
				_allowConversions = false;

				if (IsConversionReversed)
				{
					Text = $"{Conversion:G8}";
				}
				else
				{
					Text = $"{Conversion:N}";
				}

				IsConversionReversed = !IsConversionReversed;

				_allowConversions = true;

				DoConversion();
			}
		}

		private void DoConversion()
		{
			if (_allowConversions)
			{
				if (decimal.TryParse(Text, out var result) && ConversionRate > 0)
				{
					if (IsConversionReversed)
					{
						CurrencyCode = ConversionCurrencyCode;

						Conversion = result / ConversionRate;

						ConversionText = $"≈ {Conversion:G8} BTC";
					}
					else
					{
						CurrencyCode = "BTC";

						Conversion = result * ConversionRate;

						ConversionText = $"≈ {Conversion:N}" + (!string.IsNullOrWhiteSpace(ConversionCurrencyCode)
							? $" {ConversionCurrencyCode}"
							: "");
					}
				}
				else
				{
					Conversion = 0;
					ConversionText = string.Empty;
					CurrencyCode = "";
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
