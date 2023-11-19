using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Controls;
public partial class NewPrivacyBar : UserControl
{
	public NewPrivacyBar()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);

		this.WhenAnyValue(x => x.SemiPrivateAmount, x => x.PrivateAmount, selector: (a, b) => a + b).BindTo(this, x => x.SemiPrivateOverlay);
	}

	public static readonly DirectProperty<NewPrivacyBar, decimal> SemiPrivateOverlayProperty =
		AvaloniaProperty.RegisterDirect<NewPrivacyBar, decimal>(
			nameof(SemiPrivateOverlay),
			o => o.SemiPrivateOverlay);

	private decimal _semiPrivateOverlay;

	public decimal SemiPrivateOverlay
	{
		get { return _semiPrivateOverlay; }
		private set { SetAndRaise(SemiPrivateOverlayProperty, ref _semiPrivateOverlay, value); }
	}

	public static readonly StyledProperty<decimal> TotalAmountProperty = AvaloniaProperty.Register<NewPrivacyBar, decimal>("TotalAmount");

	public decimal TotalAmount
	{
		get => GetValue(TotalAmountProperty);
		set => SetValue(TotalAmountProperty, value);
	}

	public static readonly StyledProperty<decimal> PrivateAmountProperty = AvaloniaProperty.Register<NewPrivacyBar, decimal>("PrivateAmount");

	public decimal PrivateAmount
	{
		get => GetValue(PrivateAmountProperty);
		set => SetValue(PrivateAmountProperty, value);
	}

	public static readonly StyledProperty<decimal> SemiPrivateAmountProperty = AvaloniaProperty.Register<NewPrivacyBar, decimal>("SemiPrivateAmount");

	public decimal SemiPrivateAmount
	{
		get => GetValue(SemiPrivateAmountProperty);
		set => SetValue(SemiPrivateAmountProperty, value);
	}
}
