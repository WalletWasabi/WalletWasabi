using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets.CoinJoinPayment;

public partial class AddCoinJoinPaymentView : UserControl
{
	public AddCoinJoinPaymentView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
