using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Views.NavBar;

public class NavBarBox : SelectingItemsControl
{
	private static readonly FuncTemplate<IPanel> DefaultPanel =
		new FuncTemplate<IPanel>(() => new StackPanel());

	static NavBarBox()
	{
		SelectionModeProperty.OverrideDefaultValue<NavBarBox>(SelectionMode.AlwaysSelected);
		ItemsPanelProperty.OverrideDefaultValue<NavBarBox>(DefaultPanel);
	}

	/// <inheritdoc/>
	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);

		if (e.NavigationMethod == NavigationMethod.Directional)
		{
			e.Handled = UpdateSelectionFromEventSource(e.Source);
		}
	}

	/// <inheritdoc/>
	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);

		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Pointer.Type == PointerType.Mouse)
		{
			e.Handled = UpdateSelectionFromEventSource(e.Source);
		}
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		if (e.InitialPressMouseButton == MouseButton.Left && e.Pointer.Type != PointerType.Mouse)
		{
			var container = GetContainerFromEventSource(e.Source);
			if (container != null
			    && container.GetVisualsAt(e.GetPosition(container))
				    .Any(c => container == c || container.IsVisualAncestorOf(c)))
			{
				e.Handled = UpdateSelectionFromEventSource(e.Source);
			}
		}
	}
}
