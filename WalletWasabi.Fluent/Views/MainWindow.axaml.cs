using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views;

public class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		this.AttachDevTools();
#endif
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);

		if (!Services.UiConfig.HideOnClose && Application.Current?.DataContext is ApplicationViewModel avm)
		{
			e.Cancel = !avm.CanShutdown();

			if (e.Cancel)
			{
				avm.OnClosePrevented();
			}
		}
	}
}
