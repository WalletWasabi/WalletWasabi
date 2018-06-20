using Avalonia;
using Avalonia.Markup.Xaml;
using AvalonStudio.Shell.Controls;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow
	{
		public MainWindow()
		{
			InitializeComponent();

			this.AttachDevTools();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
