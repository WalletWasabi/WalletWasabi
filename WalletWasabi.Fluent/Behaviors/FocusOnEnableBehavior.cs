using Avalonia;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusOnEnableBehavior : DisposingBehavior<Button>
	{
		protected override void OnAttached(CompositeDisposable disposables)
		{
			AssociatedObject
				.GetObservable(InputElement.IsEffectivelyEnabledProperty)
				.Where(x => x)
				.Subscribe(_ => AssociatedObject!.Focus())
				.DisposeWith(disposables);
		}
	}
}