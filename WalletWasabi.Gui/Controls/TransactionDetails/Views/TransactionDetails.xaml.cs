using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.TransactionDetails.Views
{
	public class TransactionDetails : UserControl
	{
		public TransactionDetails()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
