using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Controls;

public class DualCurrencyEntryBox : ContentControl
{
	public static readonly StyledProperty<CurrencyEntryBox> LeftEntryBoxProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, CurrencyEntryBox>(nameof(LeftEntryBox));

	public static readonly StyledProperty<CurrencyEntryBox> RightEntryBoxProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, CurrencyEntryBox>(nameof(RightEntryBox));

	public static readonly StyledProperty<bool> IsConversionReversedProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsConversionReversed), defaultBindingMode: BindingMode.TwoWay);

	public static readonly StyledProperty<bool> IsConversionAvailableProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsConversionAvailable));

	public DualCurrencyEntryBox()
	{
		this.GetObservable(IsConversionReversedProperty)
			.Do(_ => LeftEntryBox?.Focus())
			.Subscribe();
	}

	public CurrencyEntryBox LeftEntryBox
	{
		get => GetValue(LeftEntryBoxProperty);
		set => SetValue(LeftEntryBoxProperty, value);
	}

	public CurrencyEntryBox RightEntryBox
	{
		get => GetValue(RightEntryBoxProperty);
		set => SetValue(RightEntryBoxProperty, value);
	}

	public bool IsConversionReversed
	{
		get => GetValue(IsConversionReversedProperty);
		set => SetValue(IsConversionReversedProperty, value);
	}

	public bool IsConversionAvailable
	{
		get => GetValue(IsConversionAvailableProperty);
		set => SetValue(IsConversionAvailableProperty, value);
	}
}
