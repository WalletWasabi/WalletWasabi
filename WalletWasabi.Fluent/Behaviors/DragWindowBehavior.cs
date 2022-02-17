using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class DragWindowBehavior : AttachedToVisualTreeBehavior<Control>
{
	private IDisposable? _onDrag;

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

		_onDrag =
			Observable.FromEventPattern<PointerPressedEventArgs>(AssociatedObject, nameof(Control.PointerPressed))
					  .Where(e => e.EventArgs.InputModifiers == InputModifiers.LeftMouseButton)
				      .Subscribe(e => window.BeginMoveDrag(e.EventArgs));
	}

	protected override void OnDetaching()
	{
		_onDrag?.Dispose();
	}
}
