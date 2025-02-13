using Avalonia;
using Avalonia.Controls.Primitives;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Controls;

public class AmountControl : TemplatedControl
{
	public static readonly StyledProperty<Amount?> AmountProperty =
		AvaloniaProperty.Register<AmountControl, Amount?>(nameof(Amount));

	public static readonly DirectProperty<AmountControl, bool> IsPositiveProperty =
		AvaloniaProperty.RegisterDirect<AmountControl, bool>(
			nameof(IsPositive),
			o => o.IsPositive);

	public static readonly DirectProperty<AmountControl, bool> IsNegativeProperty =
		AvaloniaProperty.RegisterDirect<AmountControl, bool>(
			nameof(IsNegative),
			o => o.IsNegative);

	private bool _isPositive;
	private bool _isNegative;

	public AmountControl()
	{
		this.GetObservable(AmountProperty).Subscribe(UpdateDerivedProperties);
	}

	public Amount? Amount
	{
		get => GetValue(AmountProperty);
		set => SetValue(AmountProperty, value);
	}

	public bool IsPositive
	{
		get => _isPositive;
		private set => SetAndRaise(IsPositiveProperty, ref _isPositive, value);
	}

	public bool IsNegative
	{
		get => _isNegative;
		private set => SetAndRaise(IsNegativeProperty, ref _isNegative, value);
	}

	private void UpdateDerivedProperties(Amount? amount)
	{
		IsPositive = amount is not null && amount.Btc > Money.Zero;
		IsNegative = amount is null || amount.Btc <= Money.Zero;
	}
}
