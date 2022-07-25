using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;

public partial class BtcPaymentControl : UserControl
{
	public static readonly DirectProperty<BtcPaymentControl, PaymentViewModel> PaymentControllerProperty = AvaloniaProperty.RegisterDirect<BtcPaymentControl, PaymentViewModel>(
		"PaymentController",
		o => o.PaymentController,
		(o, v) => o.PaymentController = v);

	private PaymentViewModel _paymentController;

	public PaymentViewModel PaymentController
	{
		get => _paymentController;
		set => SetAndRaise(PaymentControllerProperty, ref _paymentController, value);
	}

	public BtcPaymentControl()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private ScanQrViewModel _scanQrController;

	public static readonly DirectProperty<BtcPaymentControl, ScanQrViewModel> ScanQrControllerProperty = AvaloniaProperty.RegisterDirect<BtcPaymentControl, ScanQrViewModel>(
		"ScanQrController",
		o => o.ScanQrController,
		(o, v) => o.ScanQrController = v);

	public ScanQrViewModel ScanQrController
	{
		get => _scanQrController;
		set => SetAndRaise(ScanQrControllerProperty, ref _scanQrController, value);
	}

	private PasteButtonViewModel _pasteController;

	public static readonly DirectProperty<BtcPaymentControl, PasteButtonViewModel> PasteControllerProperty = AvaloniaProperty.RegisterDirect<BtcPaymentControl, PasteButtonViewModel>(
		"PasteController",
		o => o.PasteController,
		(o, v) => o.PasteController = v);

	public PasteButtonViewModel PasteController
	{
		get => _pasteController;
		set => SetAndRaise(PasteControllerProperty, ref _pasteController, value);
	}
}
