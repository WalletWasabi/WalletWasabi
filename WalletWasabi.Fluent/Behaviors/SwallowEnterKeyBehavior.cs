using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Behaviors;

internal class SwallowEnterKeyBehavior : AttachedToVisualTreeBehavior<InputElement>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern<KeyEventArgs>(AssociatedObject, nameof(InputElement.KeyDown))
			.Where(args => args.EventArgs.Key == Key.Enter)
			.Subscribe(r => r.EventArgs.Handled = true)
			.DisposeWith(disposable);
	}
}
