using Avalonia.Controls;
using Avalonia.Input;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	public class CommandOnPointerEnterLeaveBehavior : CommandBasedBehavior<Control>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables?.Dispose();

			Disposables = new CompositeDisposable
			{
				AssociatedObject.AddHandler(InputElement.PointerEnterEvent,
					(sender, e) =>
					{
						CommandParameter = true;
						e.Handled = ExecuteCommand();
					}),
				AssociatedObject.AddHandler(InputElement.PointerLeaveEvent,
					(sender, e) =>
					{
						CommandParameter = false;
						e.Handled = ExecuteCommand();
					})
			};
			base.OnAttached();
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
			Disposables = null;
		}
	}
}
