using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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

	protected override Size MeasureOverride(Size availableSize)
	{
		Console.WriteLine($"[MainWindow.MeasureOverride] availableSize='{availableSize}'");
		try
		{
			var result = base.MeasureOverride(availableSize);
			Console.WriteLine($"[MainWindow.MeasureOverride] result='{result}'");
			return result;
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		Console.WriteLine($"[MainWindow.ArrangeOverride] finalSize='{finalSize}'");
		try
		{
			var result = base.ArrangeOverride(finalSize);
			Console.WriteLine($"[MainWindow.ArrangeOverride] result='{result}'");
			return result;
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}
}
