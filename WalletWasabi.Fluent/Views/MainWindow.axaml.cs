using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views
{
	public class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			Renderer.DrawFps = true;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
#if DEBUG
			this.AttachDevTools();
#endif
		}
	}
}
