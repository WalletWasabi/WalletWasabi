using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class WindowClosingBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.Closing))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				// TODO: Prevent window closing when dialog is open.
			})
			.DisposeWith(disposables);
	}
}
