using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.Components;

public class QrCodeButton : UserControl
{
	public static readonly DirectProperty<QrCodeButton, string> AddressProperty =
		AvaloniaProperty.RegisterDirect<QrCodeButton, string>(
			"Address",
			o => o.Address,
			(o, v) => o.Address = v);

	public static readonly DirectProperty<QrCodeButton, ScanQrViewModel> ControllerProperty =
		AvaloniaProperty.RegisterDirect<QrCodeButton, ScanQrViewModel>(
			"Controller",
			o => o.Controller,
			(o, v) => o.Controller = v);

	private string _address;

	private ScanQrViewModel _controller;

	public QrCodeButton()
	{
		InitializeComponent();

		this.WhenAnyValue(x => x.Controller.ScanQrCommand)
			.SelectMany(x => x)
			.Subscribe(x => Address = x);
	}

	public ScanQrViewModel Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	public string Address
	{
		get => _address;
		set => SetAndRaise(AddressProperty, ref _address, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
