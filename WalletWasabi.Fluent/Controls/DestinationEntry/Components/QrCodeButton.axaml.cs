using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;

public class QrCodeButton : UserControl
{
	public static readonly DirectProperty<QrCodeButton, string> AddressProperty =
		AvaloniaProperty.RegisterDirect<QrCodeButton, string>(
			"Address",
			o => o.Address,
			(o, v) => o.Address = v);

	private string _address;

	public QrCodeButton()
	{
		InitializeComponent();

		this.WhenAnyValue(x => x.ScanQrController.ScanQrCommand)
			.SelectMany(x => x)
			.Subscribe(x => Address = x);
	}

	private ScanQrViewModel _scanQrController;

	public static readonly DirectProperty<QrCodeButton, ScanQrViewModel> ScanQrControllerProperty = AvaloniaProperty.RegisterDirect<QrCodeButton, ScanQrViewModel>(
		"ScanQrController",
		o => o.ScanQrController,
		(o, v) => o.ScanQrController = v);

	public ScanQrViewModel ScanQrController
	{
		get => _scanQrController;
		set => SetAndRaise(ScanQrControllerProperty, ref _scanQrController, value);
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
