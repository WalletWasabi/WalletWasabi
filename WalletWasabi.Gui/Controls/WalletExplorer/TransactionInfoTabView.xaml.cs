using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionInfoTabView : UserControl
	{
		public TransactionInfoTabView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
