using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Controls.Payment.Components;

public class AddressEntryControl : UserControl
{
	public static readonly StyledProperty<string> WatermarkProperty =
		AvaloniaProperty.Register<AddressEntryControl, string>("Watermark");

	public static readonly DirectProperty<AddressEntryControl, PaymentViewModel> ControllerProperty =
		AvaloniaProperty.RegisterDirect<AddressEntryControl, PaymentViewModel>(
			"Controller",
			o => o.Controller,
			(o, v) => o.Controller = v);

	private PaymentViewModel _controller;

	public AddressEntryControl()
	{
		InitializeComponent();
	}

	public PaymentViewModel Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	public string Watermark
	{
		get => GetValue(WatermarkProperty);
		set => SetValue(WatermarkProperty, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
