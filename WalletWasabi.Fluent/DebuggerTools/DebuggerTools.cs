using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Fluent.DebuggerTools.ViewModels;
using WalletWasabi.Fluent.DebuggerTools.Views;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.DebuggerTools;

internal static class DebuggerTools
{
	public static void AttachDebuggerTools(this TopLevel root)
	{
		AttachDebuggerTools(root, new KeyGesture(Key.F5));
	}

	public static void AttachDebuggerTools(this TopLevel root, KeyGesture gesture)
	{
		void Handler(object? sender, KeyEventArgs args)
		{
			if (gesture.Matches(args))
			{
				var mainViewModel = root.DataContext as MainViewModel;
				if (mainViewModel is null)
				{
					return;
				}

				var debuggerViewModel = new DebuggerViewModel(mainViewModel);

				var window = new DebuggerWindow
				{
					DataContext = debuggerViewModel
				};

				window.Show(root as Window);
			}
		}

		root.AddHandler(InputElement.KeyDownEvent, Handler, RoutingStrategies.Tunnel);
	}
}
