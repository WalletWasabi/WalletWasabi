using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent;

public static class Diagnostics
{
	private static DiagnosticsWindow? Window;

	public static void AttachDiagnostics(this TopLevel root)
	{
		AttachDiagnostics(root, new(Key.F7));
	}

	public static void AttachDiagnostics(this TopLevel root, KeyGesture gesture)
	{
		async void Handler(object? sender, KeyEventArgs args)
		{
			if (gesture.Matches(args))
			{
				if (Window is { })
				{
					Window.Activate();
				}
				else
				{
					Window = new DiagnosticsWindow(root)
					{
						Width = 300,
						Height = 300,
						WindowStartupLocation = WindowStartupLocation.Manual,
						WindowState = WindowState.Normal,
						Position = new PixelPoint(0, 0)
					};
					Window.Show();
					Window.Closed += WindowOnClosed;
				}
			}
		}

		root.AddHandler(InputElement.KeyDownEvent, Handler, RoutingStrategies.Tunnel);
	}

	private static void WindowOnClosed(object? sender, EventArgs e)
	{
		if (Window is { })
		{
			Window.Closed -= WindowOnClosed;
			Window = null;
		}
	}
}
