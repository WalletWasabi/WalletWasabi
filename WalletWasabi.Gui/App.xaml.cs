using Avalonia;
using Avalonia.Markup.Xaml;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui
{
	public class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

 		public static new App Current => (App)Application.Current;

		public KeyManager KeyManager { get; set; }
	}
}
