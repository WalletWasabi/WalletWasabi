using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.SearchBar;

public partial class SearchBar : UserControl
{
	public SearchBar()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Unfocus()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
		{
			mainWindow.Focus();
		}
	}
}
