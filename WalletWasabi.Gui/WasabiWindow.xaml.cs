using Avalonia;
using Avalonia.Markup.Xaml;
using AvalonStudio.Shell.Controls;

namespace WalletWasabi.Gui
{
	public class WasabiWindow : MetroWindow
	{
		public WasabiWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
