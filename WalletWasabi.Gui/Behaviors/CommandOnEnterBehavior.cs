using Avalonia.Controls;
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

			Disposables.Add(AssociatedObject.AddHandler(TextBox.KeyDownEvent, (sender, e) =>
			{
				if (e.Key == Avalonia.Input.Key.Enter)
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
