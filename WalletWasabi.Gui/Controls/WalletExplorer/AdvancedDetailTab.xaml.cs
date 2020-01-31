using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class AdvancedDetailTab : UserControl
	{
		public AdvancedDetailTab()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
