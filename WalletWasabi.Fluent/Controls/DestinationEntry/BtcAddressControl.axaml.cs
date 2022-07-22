using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

namespace WalletWasabi.Fluent.Controls.DestinationEntry
{
	public partial class BtcAddressControl : UserControl
	{
		public static readonly DirectProperty<BtcAddressControl, PaymentViewModel> PaymentControllerProperty = AvaloniaProperty.RegisterDirect<BtcAddressControl, PaymentViewModel>(
			"PaymentController",
			o => o.PaymentController,
			(o, v) => o.PaymentController = v);

		private PaymentViewModel _paymentController;

		public PaymentViewModel PaymentController
		{
			get => _paymentController;
			set => SetAndRaise(PaymentControllerProperty, ref _paymentController, value);
		}

		public BtcAddressControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
