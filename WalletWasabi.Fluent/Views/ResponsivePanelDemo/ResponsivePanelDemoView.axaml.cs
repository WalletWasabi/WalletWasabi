using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.ResponsivePanelDemo
{
	public class ResponsivePanelDemoView : UserControl
	{
		public ResponsivePanelDemoView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}