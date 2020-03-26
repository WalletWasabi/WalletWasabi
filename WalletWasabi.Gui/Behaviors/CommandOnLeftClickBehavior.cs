using Avalonia.Input;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	public class CommandOnLeftClickBehavior : CommandBasedBehavior<InputElement>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Disposables.Add(AssociatedObject.AddHandler(InputElement.PointerPressedEvent, (sender, e) =>
			{
				if (e.Pointer.Type == PointerType.Mouse)
				{
					var properties = e.GetCurrentPoint(AssociatedObject).Properties;
					if (properties.IsLeftButtonPressed)
					{
						e.Handled = ExecuteCommand();
					}
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
