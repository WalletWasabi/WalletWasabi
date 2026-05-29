using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Dialogs;

public partial class ShowQrCameraDialogView : UserControl
{
	// NASTY HACK! DO NOT USE IN PRODUCTION. kiminuo
	public static volatile Image? QrImage;

	public ShowQrCameraDialogView()
	{
		InitializeComponent();
		QrImage = this.FindControl<Image>("qrImage");
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
