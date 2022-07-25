using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;

public class BtcAndPayjoinPaymentControl : UserControl
{
	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, decimal> ConversionRateProperty =
		AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, decimal>(
			"ConversionRate",
			o => o.ConversionRate,
			(o, v) => o.ConversionRate = v);

	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, string> AddressProperty =
		AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, string>(
			"Address",
			o => o.Address,
			(o, v) => o.Address = v);

	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, PaymentViewModel> ControllerProperty =
		AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, PaymentViewModel>(
			"Controller",
			o => o.Controller,
			(o, v) => o.Controller = v);

	public static readonly DirectProperty<BtcAndPayjoinPaymentControl, decimal> AmountProperty =
		AvaloniaProperty.RegisterDirect<BtcAndPayjoinPaymentControl, decimal>(
			"Amount",
			o => o.Amount,
			(o, v) => o.Amount = v);

	private readonly CompositeDisposable _disposables = new();

	private string _address;

	private decimal _amount;

	private PaymentViewModel _controller;

	private decimal _conversionRate;

	public BtcAndPayjoinPaymentControl()
	{
		InitializeComponent();
	}

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

	public PaymentViewModel Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	public decimal Amount
	{
		get => _amount;
		set => SetAndRaise(AmountProperty, ref _amount, value);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		this.WhenAnyObservable(x => x.Controller.AddressController.ParsedAddress)
			.WhereNotNull()
			.Do(a => Address = a.BtcAddress)
			.Subscribe()
			.DisposeWith(_disposables);
		
		this.WhenAnyValue(x => x.Controller.AmountController.Amount)
			.WhereNotNull()
			.Do(a => Amount = a)
			.Subscribe()
			.DisposeWith(_disposables);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
