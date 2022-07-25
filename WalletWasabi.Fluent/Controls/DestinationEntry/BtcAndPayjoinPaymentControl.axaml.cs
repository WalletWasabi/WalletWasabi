using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;

public class BtcAndPayjoinPaymentControl : UserControl
{
	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, decimal> ConversionRateProperty =
		AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, decimal>(
			"ConversionRate",
			o => o.ConversionRate,
			(o, v) => o.ConversionRate = v);
	
	private decimal _conversionRate;

	public BtcAndPayjoinPaymentControl()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		DisposableMixin.DisposeWith(
				this.WhenAnyValue(x => x.Controller.PaymentViewModel.Address)
					.Do(a => Address = a)
					.Subscribe(), disposables);
	}

	private string _address;

	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, string> AddressProperty = AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, string>(
		"Address",
		o => o.Address,
		(o, v) => o.Address = v);

	public string Address
	{
		get => _address;
		set => SetAndRaise(AddressProperty, ref _address, value);
	}

	public decimal ConversionRate
	{
		get => _conversionRate;
		set => SetAndRaise(ConversionRateProperty, ref _conversionRate, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private BigController _controller;

	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, BigController> ControllerProperty = AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, BigController>(
		"Controller",
		o => o.Controller,
		(o, v) => o.Controller = v);

	private CompositeDisposable disposables = new();

	public BigController Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	private double _amount;

	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, double> AmountProperty = AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, double>(
		"Amount",
		o => o.Amount,
		(o, v) => o.Amount = v);

	public double Amount
	{
		get => _amount;
		set => SetAndRaise(AmountProperty, ref _amount, value);
	}
}
