using Avalonia;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Controls;

public class AmountControl : TemplatedControl
{
	public static readonly StyledProperty<Amount?> AmountProperty = AvaloniaProperty.Register<AmountControl, Amount?>(nameof(Amount));

	public Amount? Amount
	{
		get => GetValue(AmountProperty);
		set => SetValue(AmountProperty, value);
	}
}
