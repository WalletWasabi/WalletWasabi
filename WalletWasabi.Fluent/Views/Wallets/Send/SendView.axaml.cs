using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets.Send
{
	public class SendView : UserControl
	{
		// NASTY HACK! DO NOT USE IN PRODUCTION. kiminuo
		public static volatile Image? QrImage;

		public SendView()
		{
			InitializeComponent();
			QrImage = this.FindControl<Image>("qrImage");
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
