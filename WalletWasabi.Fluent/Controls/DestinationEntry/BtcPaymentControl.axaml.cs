using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;

public class BtcPaymentControl : UserControl
{
	public static readonly DirectProperty<BtcPaymentControl, string> AddressProperty =
		AvaloniaProperty.RegisterDirect<BtcPaymentControl, string>(
			"Address",
			o => o.Address,
			(o, v) => o.Address = v);

	public static readonly DirectProperty<BtcPaymentControl, PaymentViewModel> ControllerProperty =
		AvaloniaProperty.RegisterDirect<BtcPaymentControl, PaymentViewModel>(
			"Controller",
			o => o.Controller,
			(o, v) => o.Controller = v);

	private readonly CompositeDisposable disposables = new();

	private string _address;

	private PaymentViewModel _controller;

	public BtcPaymentControl()
	{
		InitializeComponent();
	}

	public string Address
	{
		get => _address;
		set => SetAndRaise(AddressProperty, ref _address, value);
	}

	public PaymentViewModel Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		DisposableMixin.DisposeWith(
			this.WhenAnyObservable(x => x.Controller.AddressController.ParsedAddress)
				.WhereNotNull()
				.Do(a => Address = a.BtcAddress)
				.Subscribe(),
			disposables);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
