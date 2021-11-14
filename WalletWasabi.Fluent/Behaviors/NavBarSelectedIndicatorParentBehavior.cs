using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorParentBehavior : AttachedToVisualTreeBehavior<Control>
{
	public readonly CompositeDisposable disposables = new();

	public static readonly AttachedProperty<NavBarSelectedIndicatorState>
		ParentStateProperty =
			AvaloniaProperty
				.RegisterAttached<NavBarSelectedIndicatorParentBehavior, Control, NavBarSelectedIndicatorState>(
					"ParentState",
					inherits: true);

	public static NavBarSelectedIndicatorState GetParentState(Control element)
	{
		return element.GetValue(ParentStateProperty);
	}

	public static void SetParentState(Control element, NavBarSelectedIndicatorState value)
	{
		element.SetValue(ParentStateProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		var k = new NavBarSelectedIndicatorState();

		SetParentState(AssociatedObject, k);
		var z = new NavBarSelectionIndicatorAdorner(AssociatedObject, k);

		disposables.Add(k);
		disposables.Add(z);

		AssociatedObject.DetachedFromVisualTree += delegate { disposables.Dispose(); };
	}
}