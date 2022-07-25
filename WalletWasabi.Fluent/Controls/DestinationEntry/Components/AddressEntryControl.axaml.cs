using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.Components;

public class AddressEntryControl : UserControl
{
	public static readonly StyledProperty<string> WatermarkProperty =
		AvaloniaProperty.Register<AddressEntryControl, string>("Watermark");

	private BigController _controller;

	public static readonly DirectProperty<AddressEntryControl, BigController> ControllerProperty = AvaloniaProperty.RegisterDirect<AddressEntryControl, BigController>(
		"Controller",
		o => o.Controller,
		(o, v) => o.Controller = v);

	public BigController Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	public AddressEntryControl()
	{
		InitializeComponent();
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
