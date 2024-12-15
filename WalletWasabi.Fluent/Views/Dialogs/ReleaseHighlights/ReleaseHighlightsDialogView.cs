using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Dialogs.ReleaseHighlights;

public class ReleaseHighlightsDialogView : UserControl
{
	public ReleaseHighlightsDialogView()
	{
		InitializeComponent();
	}
	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
