using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Views.Wallets;

public class MobileWalletsListView : UserControl
{
	public MobileWalletsListView()
	{
		InitializeComponent();
		
		var listBox = this.FindControl<ListBox>("WalletsListBox");
		if (listBox != null)
		{
			listBox.SelectionChanged += (s, e) =>
			{
				if (listBox.SelectedItem is WalletPageViewModel wallet && DataContext is MobileWalletsListViewModel vm)
				{
					vm.SelectWallet(wallet);
					listBox.SelectedItem = null; // Reset selection so it can be tapped again
				}
			};
		}
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
