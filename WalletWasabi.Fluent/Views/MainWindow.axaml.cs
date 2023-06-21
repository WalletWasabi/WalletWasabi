using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Diagnostics;
using WalletWasabi.Fluent.Screenshot;

namespace WalletWasabi.Fluent.Views;

public class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		// TODO: This is a workaround for this Avalonia issue (https://github.com/AvaloniaUI/Avalonia/issues/11850)
		// Please, remove after it's been fixed.
		ApplyRendererWorkaround();
	}

	private void ApplyRendererWorkaround()
	{
		this.WhenAnyValue(x => x.WindowState)
			.Where(state => state == WindowState.Normal)
			.Where(_ => OperatingSystem.IsLinux())
			.Do(_ =>
			{
				Renderer.Start();
				Activate();
			})
			.Subscribe();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		this.AttachDevTools();
		this.AttachCapture();
		this.AttachDiagnostics();
#endif
	}
}
