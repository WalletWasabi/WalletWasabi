using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;

public partial class AmountColumnView : UserControl
{
	public AmountColumnView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}