using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering;
using System.ComponentModel;
using WalletWasabi.Fluent.Diagnostics;
using WalletWasabi.Fluent.Screenshot;

namespace WalletWasabi.Fluent.Views;

public class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

#if DEBUG
		AddHandler(KeyDownEvent, KeyDownHandler, RoutingStrategies.Tunnel);
#endif
	}

	public event EventHandler<CancelEventArgs>? CancelButtonPressedEvent;

	public void CancelPressed()
	{
		CancelButtonPressedEvent?.Invoke(this, new CancelEventArgs(false));
	}

	private void KeyDownHandler(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.F8)
		{
			if (RendererDiagnostics.DebugOverlays == RendererDebugOverlays.None)
			{
				RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps;
			}
			else
			{
				if (!RendererDiagnostics.DebugOverlays.HasFlag(RendererDebugOverlays.LayoutTimeGraph))
				{
					RendererDiagnostics.DebugOverlays |= RendererDebugOverlays.LayoutTimeGraph;
				}
				else if (!RendererDiagnostics.DebugOverlays.HasFlag(RendererDebugOverlays.RenderTimeGraph))
				{
					RendererDiagnostics.DebugOverlays |= RendererDebugOverlays.RenderTimeGraph;
				}
				else if (!RendererDiagnostics.DebugOverlays.HasFlag(RendererDebugOverlays.DirtyRects))
				{
					RendererDiagnostics.DebugOverlays |= RendererDebugOverlays.DirtyRects;
				}
				else
				{
					RendererDiagnostics.DebugOverlays = RendererDebugOverlays.None;
				}
			}
		}
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		this.AttachCapture();
		this.AttachDiagnostics();
#endif
	}
}
