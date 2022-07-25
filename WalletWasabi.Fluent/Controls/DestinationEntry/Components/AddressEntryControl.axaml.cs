using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;

public class AddressEntryControl : UserControl
{
	public static readonly StyledProperty<string> WatermarkProperty =
		AvaloniaProperty.Register<AddressEntryControl, string>("Watermark");

	public static readonly DirectProperty<AddressEntryControl, PasteButtonViewModel> PasteControllerProperty =
		AvaloniaProperty.RegisterDirect<AddressEntryControl, PasteButtonViewModel>(
			"PasteController",
			o => o.PasteController,
			(o, v) => o.PasteController = v);

	public static readonly DirectProperty<AddressEntryControl, ScanQrViewModel> QrControllerProperty =
		AvaloniaProperty.RegisterDirect<AddressEntryControl, ScanQrViewModel>(
			"QrController",
			o => o.QrController,
			(o, v) => o.QrController = v);

	private PasteButtonViewModel _pasteController;

	private ScanQrViewModel _qrController;

	public AddressEntryControl()
	{
		InitializeComponent();
	}

	public string Watermark
	{
		get => GetValue(WatermarkProperty);
		set => SetValue(WatermarkProperty, value);
	}

	public PasteButtonViewModel PasteController
	{
		get => _pasteController;
		set => SetAndRaise(PasteControllerProperty, ref _pasteController, value);
	}

	public ScanQrViewModel QrController
	{
		get => _qrController;
		set => SetAndRaise(QrControllerProperty, ref _qrController, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
