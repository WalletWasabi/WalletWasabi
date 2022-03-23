using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.SearchBar;

public class SearchBar : UserControl
{
	public SearchBar()
	{
		InitializeComponent();
	}

	private void RootPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		e.Handled = false;
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}