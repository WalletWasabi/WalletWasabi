using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Controls;

public class DualCurrencyEntryBox : TemplatedControl
{
	public static readonly StyledProperty<Amount?> AmountProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, Amount?>(nameof(Amount), enableDataValidation: true);

	public static readonly StyledProperty<CurrencyEntryBox?> LeftEntryBoxProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, CurrencyEntryBox?>(nameof(LeftEntryBox));

	public static readonly StyledProperty<CurrencyEntryBox?> RightEntryBoxProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, CurrencyEntryBox?>(nameof(RightEntryBox));

	public static readonly StyledProperty<bool> IsConversionReversedProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsConversionReversed), defaultBindingMode: BindingMode.TwoWay);

	public static readonly StyledProperty<bool> IsConversionAvailableProperty =
		AvaloniaProperty.Register<DualCurrencyEntryBox, bool>(nameof(IsConversionAvailable));

	public DualCurrencyEntryBox()
	{
		// Place focus on left box after toggling conversion reverse
		this.GetObservable(IsConversionReversedProperty)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(50))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(_ => LeftEntryBox?.Focus())
			.Subscribe();
	}

	public Amount? Amount
	{
		get => GetValue(AmountProperty);
		set => SetValue(AmountProperty, value);
	}

	public CurrencyEntryBox? LeftEntryBox
	{
		get => GetValue(LeftEntryBoxProperty);
		set => SetValue(LeftEntryBoxProperty, value);
	}

	public CurrencyEntryBox? RightEntryBox
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

	protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
	{
		if (property == AmountProperty)
		{
			DataValidationErrors.SetError(this, error);
		}
	}
}
