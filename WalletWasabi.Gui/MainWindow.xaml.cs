using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui
{
	internal class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			this.AttachDevTools();
			//Renderer.DrawFps = true;
			//Renderer.DrawDirtyRects = Renderer.DrawFps = true;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
