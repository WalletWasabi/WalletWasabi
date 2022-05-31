using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Behaviors;

internal class TextBoxAutoSelectTextBehavior : AttachedToVisualTreeBehavior<TextBox>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.SelectAll();
		Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.GotFocus))
			.Subscribe(_ => AssociatedObject.SelectAll())
			.DisposeWith(disposable);
		Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerReleased))
			.Subscribe(_ => AssociatedObject.SelectAll())
			.DisposeWith(disposable);;
	}
}
