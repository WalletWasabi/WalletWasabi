using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Dialogs.Authorization;

public class TrezorCoinJoinAuthDialogView : UserControl
{
	public TrezorCoinJoinAuthDialogView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
