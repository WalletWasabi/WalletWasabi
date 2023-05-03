using System.Globalization;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Controls;

public class DualCurrencyEntryBox : UserControl
{
	public static readonly DirectProperty<DualCurrencyEntryBox, decimal> AmountBtcProperty =
		AvaloniaProperty.RegisterDirect<DualCurrencyEntryBox, decimal>(
			nameof(AmountBtc),
			o => o.AmountBtc,
			(o, v) => o.AmountBtc = v,
			enableDataValidation: true,
			defaultBindingMode: BindingMode.TwoWay);

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, string>(nameof(Text));

	public static readonly StyledProperty<string> WatermarkProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, string>(nameof(Watermark));

	public static readonly StyledProperty<string> ConversionTextProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, string>(nameof(ConversionText));

	public static readonly StyledProperty<decimal> ConversionRateProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, decimal>(nameof(ConversionRate));

	public static readonly StyledProperty<string> CurrencyCodeProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, string>(nameof(CurrencyCode));

	public static readonly StyledProperty<string> ConversionCurrencyCodeProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, string>(nameof(ConversionCurrencyCode));

	public static readonly StyledProperty<string> ConversionWatermarkProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, string>(nameof(ConversionWatermark));

	public static readonly StyledProperty<bool> IsConversionReversedProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsConversionReversed));

	public static readonly StyledProperty<bool> IsConversionApproximateProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsConversionApproximate));

	public static readonly StyledProperty<bool> IsReadOnlyProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsReadOnly));

	public static readonly StyledProperty<int> LeftColumnProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, int>(nameof(LeftColumn));

	public static readonly StyledProperty<int> RightColumnProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, int>(nameof(RightColumn));

	public static readonly StyledProperty<CurrencyEntryBox?> RightEntryBoxProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, CurrencyEntryBox?>(nameof(RightEntryBox));

	public static readonly StyledProperty<CurrencyEntryBox?> LeftEntryBoxProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, CurrencyEntryBox?>(nameof(LeftEntryBox));

	private CompositeDisposable? _disposable;
	private Button? _swapButton;
	private decimal _amountBtc;
	private bool _canUpdateDisplay = true;
	private bool _canUpdateFiat = true;

	public DualCurrencyEntryBox()
	{
		this.GetObservable(TextProperty).Subscribe(InputText);
		this.GetObservable(ConversionTextProperty).Subscribe(InputConversionText);
		this.GetObservable(ConversionRateProperty).Subscribe(_ => UpdateDisplay(true));
		this.GetObservable(ConversionCurrencyCodeProperty).Subscribe(_ => UpdateDisplay(true));
		this.GetObservable(AmountBtcProperty).Subscribe(_ => UpdateDisplay(true));
		this.GetObservable(IsReadOnlyProperty).Subscribe(_ => UpdateDisplay(true));

		UpdateDisplay(false);

		PseudoClasses.Set(":noexchangerate", true);
	}

	public decimal AmountBtc
	{
		get => _amountBtc;
		set => SetAndRaise(AmountBtcProperty, ref _amountBtc, value);
	}

	public string Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public string Watermark
	{
		get => GetValue(WatermarkProperty);
		set => SetValue(WatermarkProperty, value);
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

	public string ConversionWatermark
	{
		get => GetValue(ConversionWatermarkProperty);
		set => SetValue(ConversionWatermarkProperty, value);
	}

	public bool IsConversionApproximate
	{
		get => GetValue(IsConversionApproximateProperty);
		set => SetValue(IsConversionApproximateProperty, value);
	}

	public bool IsConversionReversed
	{
		get => GetValue(IsConversionReversedProperty);
		set => SetValue(IsConversionReversedProperty, value);
	}

	public bool IsReadOnly
	{
		get => GetValue(IsReadOnlyProperty);
		set => SetValue(IsReadOnlyProperty, value);
	}

	public int LeftColumn
	{
		get => GetValue(LeftColumnProperty);
		set => SetValue(LeftColumnProperty, value);
	}

	public int RightColumn
	{
		get => GetValue(RightColumnProperty);
		set => SetValue(RightColumnProperty, value);
	}

	public CurrencyEntryBox? RightEntryBox
	{
		get => GetValue(RightEntryBoxProperty);
		set => SetValue(RightEntryBoxProperty, value);
	}

	public CurrencyEntryBox? LeftEntryBox
	{
		get => GetValue(LeftEntryBoxProperty);
		set => SetValue(LeftEntryBoxProperty, value);
	}

	protected override void OnLostFocus(RoutedEventArgs e)
	{
		base.OnLostFocus(e);

		UpdateDisplay(true);
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
			InputBtcString(text);
		}
	}

	private void InputConversionText(string text)
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
			InputFiatString(text);
		}
	}

	private void InputBtcValue(decimal value)
	{
		AmountBtc = value;
	}

	private void InputBtcString(string value)
	{
		if (CurrencyInput.TryCorrectBitcoinAmount(value, out var better) && better != Constants.MaximumNumberOfBitcoins.ToString())
		{
			value = better;
		}

		if (decimal.TryParse(value, NumberStyles.Number, CurrencyInput.InvariantNumberFormat, out var decimalValue))
		{
			InputBtcValue(decimalValue);
		}

		UpdateDisplay(false);
	}

	private void InputFiatString(string value)
	{
		if (!_canUpdateFiat)
		{
			return;
		}

		if (decimal.TryParse(value, NumberStyles.Number, CurrencyInput.InvariantNumberFormat, out var decimalValue))
		{
			InputBtcValue(FiatToBitcoin(decimalValue));
		}

		UpdateDisplay(false);
	}

	private void UpdateDisplay(bool updateTextField)
	{
		Watermark = FullFormatBtc(0);

		if (updateTextField)
		{
			_canUpdateDisplay = false;
			Text = AmountBtc > 0 ? AmountBtc.FormattedBtc() : string.Empty;

			_canUpdateDisplay = true;
		}

		UpdateDisplayFiat(updateTextField);
	}

	private void UpdateDisplayFiat(bool updateTextField)
	{
		_canUpdateFiat = false;

		if (ConversionRate == 0m)
		{
			return;
		}

		var conversion = BitcoinToFiat(AmountBtc);

		IsConversionApproximate = AmountBtc > 0;
		ConversionWatermark = FullFormatFiat(0, ConversionCurrencyCode, true);

		if (updateTextField)
		{
			ConversionText = AmountBtc > 0 ? conversion.FormattedFiat() : string.Empty;
		}

		_canUpdateFiat = true;
	}

	private decimal FiatToBitcoin(decimal fiatValue)
	{
		return fiatValue / ConversionRate;
	}

	private decimal BitcoinToFiat(decimal btcValue)
	{
		return btcValue * ConversionRate;
	}

	private static string FullFormatBtc(decimal value)
	{
		return $"{value.FormattedBtc()} BTC";
	}

	private static string FullFormatFiat(decimal value, string currencyCode, bool approximate)
	{
		var part1 = approximate ? "≈ " : "";
		var part2 = value.FormattedFiat();
		var part3 =
			!string.IsNullOrWhiteSpace(currencyCode)
				? $" {currencyCode}"
				: "";
		return part1 + part2 + part3;
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		_disposable?.Dispose();
		_disposable = new CompositeDisposable();

		_swapButton = e.NameScope.Find<Button>("PART_SwapButton");
		LeftEntryBox = e.NameScope.Find<CurrencyEntryBox>("PART_LeftEntryBox");
		RightEntryBox = e.NameScope.Find<CurrencyEntryBox>("PART_RightEntryBox");

		if (_swapButton is { })
		{
			_swapButton.Click += SwapButtonOnClick;

			_disposable.Add(Disposable.Create(() => _swapButton.Click -= SwapButtonOnClick));
		}

		ReorganizeVisuals();
	}

	private void SwapButtonOnClick(object? sender, RoutedEventArgs e)
	{
		IsConversionReversed = !IsConversionReversed;
		FocusOnLeftEntryBox();
	}

	private void FocusOnLeftEntryBox()
	{
		var focusOn =
			IsConversionReversed
				? RightEntryBox
				: LeftEntryBox;

		focusOn?.Focus();
	}

	protected override void UpdateDataValidation<T>(AvaloniaProperty<T> property, BindingValue<T> value)
	{
		if (property == AmountBtcProperty)
		{
			DataValidationErrors.SetError(this, value.Error);
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
		else if (change.Property == IsConversionReversedProperty)
		{
			PseudoClasses.Set(":reversed", change.NewValue.GetValueOrDefault<bool>());
			ReorganizeVisuals();
			UpdateDisplay(false);
		}
	}

	// this is ugly, but I couldn't find another way to make tab key and automatic focus to work properly
	// setting Grid.Column via pseudoclass based style doesn't work, not even using AffectsMeasure()... Avalonia bug?
	private void ReorganizeVisuals()
	{
		if (LeftEntryBox is { } && RightEntryBox is { })
		{
			var grid = LeftEntryBox.FindAncestorOfType<Grid>();
			grid?.Children.Remove(LeftEntryBox);
			grid?.Children.Remove(RightEntryBox);

			if (IsConversionReversed)
			{
				grid?.Children.Add(RightEntryBox);
				grid?.Children.Add(LeftEntryBox);
			}
			else
			{
				grid?.Children.Add(LeftEntryBox);
				grid?.Children.Add(RightEntryBox);
			}
		}
	}
}
