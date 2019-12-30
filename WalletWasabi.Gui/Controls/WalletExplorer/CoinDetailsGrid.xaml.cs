using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinDetailsGrid : UserControl
	{
		public CoinDetailsGrid()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
