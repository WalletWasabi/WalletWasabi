using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Behaviors;

public class DragWindowBehavior : AttachedToVisualTreeBehavior<Control>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		if (Application.Current?.ApplicationLifetime is not ClassicDesktopStyleApplicationLifetime desktop)
		{
			return;
		}
		
		Observable.FromEventPattern<PointerPressedEventArgs>(AssociatedObject, nameof(Control.PointerPressed))
			      .Where(e => e.EventArgs.InputModifiers == InputModifiers.LeftMouseButton)
				  .Subscribe(e => desktop.MainWindow.BeginMoveDrag(e.EventArgs));
	}
}
