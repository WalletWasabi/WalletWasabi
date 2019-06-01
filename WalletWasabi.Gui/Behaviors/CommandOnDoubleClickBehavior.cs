using Avalonia.Controls;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	public class CommandOnDoubleClickBehavior : CommandBasedBehavior<Control>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Disposables.Add(AssociatedObject.AddHandler(Control.PointerPressedEvent, (sender, e) =>
			{
				if (e.ClickCount == 2)
				{
					e.Handled = ExecuteCommand();
				}
			}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
