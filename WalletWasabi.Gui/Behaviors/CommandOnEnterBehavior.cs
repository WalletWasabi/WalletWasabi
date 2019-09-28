using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	public class CommandOnEnterBehavior : CommandBasedBehavior<TextBox>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Disposables.Add(AssociatedObject.AddHandler(InputElement.KeyDownEvent, (sender, e) =>
				{
					if (e.Key == Key.Enter)
					{
						e.Handled = ExecuteCommand();
					}
				}, RoutingStrategies.Tunnel));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
