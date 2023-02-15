using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedPropertyBehavior : DisposingBehavior<ListBoxItem>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is not { } || AssociatedObject.Content is not NavBarItemViewModel navItem)
		{
			return;
		}

		AssociatedObject.WhenAnyValue(x => x.IsSelected)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(x => navItem.IsSelected = x)
			.Subscribe()
			.DisposeWith(disposables);
	}
}
