using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.Screenshot;

namespace WalletWasabi.Fluent.Views;

public class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);

		var window = new DiagnosticsWindow(this)
		{
			Width = 300,
			Height = 300,
			WindowStartupLocation = WindowStartupLocation.Manual,
			WindowState = WindowState.Normal,
			Position = new PixelPoint(0, 0)
		};
		window.Show(this);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		this.AttachDevTools();
		this.AttachCapture();
#endif
	}
}
