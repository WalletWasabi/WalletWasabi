using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class NewPrivacyBar : UserControl
{
	public static readonly DirectProperty<NewPrivacyBar, decimal> PrivateAndSemiPrivateAmountProperty =
		AvaloniaProperty.RegisterDirect<NewPrivacyBar, decimal>(
			nameof(PrivateAndSemiPrivateAmount),
			o => o.PrivateAndSemiPrivateAmount);

	public static readonly StyledProperty<decimal> TotalAmountProperty = AvaloniaProperty.Register<NewPrivacyBar, decimal>(nameof(TotalAmount));

	public static readonly StyledProperty<decimal> PrivateAmountProperty = AvaloniaProperty.Register<NewPrivacyBar, decimal>(nameof(PrivateAmount));

	public static readonly StyledProperty<decimal> SemiPrivateAmountProperty = AvaloniaProperty.Register<NewPrivacyBar, decimal>(nameof(SemiPrivateAmount));

	private decimal _privateAndSemiPrivateAmount;

	public NewPrivacyBar()
	{
		InitializeComponent();
	}

	public decimal PrivateAndSemiPrivateAmount
	{
		get => _privateAndSemiPrivateAmount;
		private set => SetAndRaise(PrivateAndSemiPrivateAmountProperty, ref _privateAndSemiPrivateAmount, value);
	}

	public decimal TotalAmount
	{
		get => GetValue(TotalAmountProperty);
		set => SetValue(TotalAmountProperty, value);
	}

	public decimal PrivateAmount
	{
		get => GetValue(PrivateAmountProperty);
		set => SetValue(PrivateAmountProperty, value);
	}

	public decimal SemiPrivateAmount
	{
		get => GetValue(SemiPrivateAmountProperty);
		set => SetValue(SemiPrivateAmountProperty, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);

		this.WhenAnyValue(x => x.SemiPrivateAmount, x => x.PrivateAmount, selector: (a, b) => a + b).BindTo(this, x => x.PrivateAndSemiPrivateAmount);
	}
}
