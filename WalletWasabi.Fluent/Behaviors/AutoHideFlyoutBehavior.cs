using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

/// <summary>
/// The behavior intend to fix https://github.com/zkSNACKs/WalletWasabi/issues/10181.
/// </summary>
public class AutoHideFlyoutBehavior : DisposingBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		AssociatedObject?
			.WhenAnyValue(x => x.IsActive, x => x.IsPointerOver, (isActive, isPointerOver) => !isActive && !isPointerOver)
			.Where(x => x)
			.Subscribe(_ => HideAllFlyout(AssociatedObject))
			.DisposeWith(disposables);
	}

	private void HideAllFlyout(Control parent)
	{
		FlyoutBase.GetAttachedFlyout(parent)?.Hide();

		foreach (var child in parent.GetVisualChildren().OfType<Control>())
		{
			HideAllFlyout(child);
		}
	}
}
