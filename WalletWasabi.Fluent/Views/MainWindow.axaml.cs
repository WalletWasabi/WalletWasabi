using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.DebuggerTools;

namespace WalletWasabi.Fluent.Views;

public class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		this.AttachDevTools();
		this.AttachDebuggerTools();
#endif
	}
}
