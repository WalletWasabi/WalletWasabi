using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Tabs
{
	internal class CustomerSupportView : UserControl
	{
		public CustomerSupportView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
