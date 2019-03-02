using Avalonia.Controls;
using Avalonia.Input;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	public class CommandOnClickBehavior : CommandBasedBehavior<InputElement>
	{
		private CompositeDisposable _disposables;

		protected override void OnAttached()
		{
			_disposables = new CompositeDisposable();

			base.OnAttached();

			_disposables.Add(AssociatedObject.AddHandler(InputElement.PointerPressedEvent, (sender, e) =>
			{
				e.Handled = ExecuteCommand();
			}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposables.Dispose();
		}
	}
}
