using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using WalletWasabi.Fluent.QRCodeDecoder;

namespace WalletWasabi.Fluent.Views
{
	public class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
			this.AttachDevTools();

			QRDecoder decoder = new();
			string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
			string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";

			using (var fs = File.OpenRead("AddressTest1.png"))
			{
				using (var bmp = WriteableBitmap.Decode(fs))
				{
					var dataCollection = decoder.SearchQrCodes(bmp);
				}
			}

			using (var fs = File.OpenRead("AddressTest2.png"))
			{
				using (var bmp = WriteableBitmap.Decode(fs))
				{
					var dataCollection = decoder.SearchQrCodes(bmp);
				}
			}
		}
	}
}