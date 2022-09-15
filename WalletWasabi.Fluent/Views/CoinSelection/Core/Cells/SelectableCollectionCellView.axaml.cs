using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.CoinSelection.Core.Cells;

public class SelectableCollectionCellView : UserControl
{
	public SelectableCollectionCellView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
