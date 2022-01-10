using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.ResponsivePanelDemo;

public class ResponsiveLayoutDemoView : UserControl
{
	public ResponsiveLayoutDemoView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
