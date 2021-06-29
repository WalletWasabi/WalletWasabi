using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Behaviors
{
	public class ShowFlyoutOnPointerOverBehavior : DisposingBehavior<Control>
	{
		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			Observable
				.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerEnter))
				.Subscribe(_ => FlyoutBase.ShowAttachedFlyout(AssociatedObject))
				.DisposeWith(disposables);
		}
	}
}
