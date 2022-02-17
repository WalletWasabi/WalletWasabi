using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class DragWindowBehavior : AttachedToVisualTreeBehavior<Control>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		if (AssociatedObject.FindAncestorOfType<Window>() is not { } window)
		{
			return;
		}

		Observable.FromEventPattern<PointerPressedEventArgs>(AssociatedObject, nameof(Control.PointerPressed))
					.Where(e => e.EventArgs.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
				    .Subscribe(e => window.BeginMoveDrag(e.EventArgs))
					.DisposeWith(disposable);
	}
}
