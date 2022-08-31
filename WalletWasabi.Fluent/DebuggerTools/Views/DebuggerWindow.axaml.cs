using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.DebuggerTools.Views;

internal partial class DebuggerWindow : Window
{
	public DebuggerWindow()
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
