using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class ButtonInsideListBoxItemBehavior : DisposingBehavior<Button>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable.FromEventPattern(AssociatedObject, nameof(Button.Click))
				  .Subscribe(x =>
				  {
					  if (x.Sender is Button button && button.FindAncestorOfType<ListBoxItem>() is { } listBoxItem)
					  {
						  listBoxItem.IsSelected = true;
					  }
				  })
				  .DisposeWith(disposables);
	}
}
