using Avalonia;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Controls;
public class AmountControl : TemplatedControl
{
	public static readonly StyledProperty<BtcAmount> AmountProperty = AvaloniaProperty.Register<AmountControl, BtcAmount>("Amount");

	public BtcAmount Amount
	{
		get => GetValue(AmountProperty);
		set => SetValue(AmountProperty, value);
	}
}
