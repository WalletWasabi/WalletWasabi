using Avalonia;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui
{
	public class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
