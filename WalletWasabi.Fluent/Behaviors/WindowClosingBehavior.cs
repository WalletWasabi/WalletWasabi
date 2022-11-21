using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class WindowClosingBehavior : DisposingBehavior<Window>
{
	public static readonly StyledProperty<bool> CancelWindowClosingProperty =
		AvaloniaProperty.Register<WindowClosingBehavior, bool>(nameof(CancelWindowClosing));

	public bool CancelWindowClosing
	{
		get => GetValue(CancelWindowClosingProperty);
		set => SetValue(CancelWindowClosingProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern<CancelEventArgs>(AssociatedObject, nameof(AssociatedObject.Closing))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(e =>
			{
				if (CancelWindowClosing)
				{
					e.Cancel = true;
					// TODO: Show notification that window closing was canceled?
				}
			})
			.DisposeWith(disposables);
	}
}
