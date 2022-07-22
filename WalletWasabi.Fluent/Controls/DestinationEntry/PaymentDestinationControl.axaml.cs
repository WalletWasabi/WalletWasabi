using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

namespace WalletWasabi.Fluent.Controls.DestinationEntry
{
	public partial class PaymentDestinationControl : UserControl
	{
		private decimal _conversionRate;

		public static readonly DirectProperty<PaymentDestinationControl, decimal> ConversionRateProperty = AvaloniaProperty.RegisterDirect<PaymentDestinationControl, decimal>(
			"ConversionRate",
			o => o.ConversionRate,
			(o, v) => o.ConversionRate = v);

		private PaymentViewModel _paymentController;

		public static readonly DirectProperty<PaymentDestinationControl, PaymentViewModel> PaymentControllerProperty = AvaloniaProperty.RegisterDirect<PaymentDestinationControl, PaymentViewModel>(
			"PaymentController",
			o => o.PaymentController,
			(o, v) => o.PaymentController = v);

		public PaymentViewModel PaymentController
		{
			get => _paymentController;
			set => SetAndRaise(PaymentControllerProperty, ref _paymentController, value);
		}

		public decimal ConversionRate
		{
			get => _conversionRate;
			set => SetAndRaise(ConversionRateProperty, ref _conversionRate, value);
		}

		public PaymentDestinationControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
