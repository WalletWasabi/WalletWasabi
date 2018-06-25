using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace WalletWasabi.Gui
{
	public class App : Application
	{
		public override void Initialize()
		{
			Dispatcher.UIThread.VerifyAccess();
			AvaloniaXamlLoader.Load(this);
		}
	}
}
