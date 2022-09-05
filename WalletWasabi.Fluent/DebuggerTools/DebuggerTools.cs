using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Fluent.DebuggerTools.ViewModels;
using WalletWasabi.Fluent.DebuggerTools.Views;

namespace WalletWasabi.Fluent.DebuggerTools;

internal static class DebuggerTools
{
	public static void AttachDebuggerTools(this TopLevel root)
	{
		AttachDebuggerTools(root, new KeyGesture(Key.F5));
	}

	public static void AttachDebuggerTools(this TopLevel root, KeyGesture gesture)
	{
		async void Handler(object? sender, KeyEventArgs args)
		{
			if (gesture.Matches(args))
			{
				var debuggerToolsViewModel = new DebuggerToolsViewModel();

				var window = new DebuggerWindow
				{
					DataContext = debuggerToolsViewModel
				};

				// window.Show(root as Window);
				window.Show();

				await Task.Run(() => debuggerToolsViewModel.Initialize());
			}
		}

		root.AddHandler(InputElement.KeyDownEvent, Handler, RoutingStrategies.Tunnel);
	}
}
