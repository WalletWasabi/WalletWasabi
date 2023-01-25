using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls;

/// <summary>
/// Container for NavBarItems.
/// </summary>
[PseudoClasses(":horizontal", ":vertical", ":selectable", ":selected")]
public class NavBarItem : Button
{
	public static readonly StyledProperty<IconElement> IconProperty =
		AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

	public static readonly StyledProperty<Orientation> IndicatorOrientationProperty =
		AvaloniaProperty.Register<NavBarItem, Orientation>(nameof(IndicatorOrientation), Orientation.Vertical);

	public static readonly StyledProperty<bool> IsSelectableProperty =
		AvaloniaProperty.Register<NavBarItem, bool>(nameof(IsSelectable));

	public static readonly StyledProperty<bool> IsSelectedProperty =
		AvaloniaProperty.Register<NavBarItem, bool>(nameof(IsSelected));

	public NavBarItem()
	{
		UpdateIndicatorOrientationPseudoClasses(IndicatorOrientation);
		UpdatePseudoClass(":selectable", IsSelectable);
	}

	/// <summary>
	/// The icon to be shown beside the header text of the item.
	/// </summary>
	public IconElement Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	/// <summary>
	/// Gets or sets the indicator orientation.
	/// </summary>
	public Orientation IndicatorOrientation
	{
		get => GetValue(IndicatorOrientationProperty);
		set => SetValue(IndicatorOrientationProperty, value);
	}

	/// <summary>
	/// Gets or sets flag indicating whether item supports selected state.
	/// </summary>
	public bool IsSelectable
	{
		get => GetValue(IsSelectableProperty);
		set => SetValue(IsSelectableProperty, value);
	}

	/// <summary>
	/// Gets or sets if the item is selected or not.
	/// </summary>
	public bool IsSelected
	{
		get => GetValue(IsSelectedProperty);
		set => SetValue(IsSelectedProperty, value);
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IndicatorOrientationProperty)
		{
			UpdateIndicatorOrientationPseudoClasses(change.NewValue.GetValueOrDefault<Orientation>());
		}

		if (change.Property == IsSelectableProperty)
		{
			UpdatePseudoClass(":selectable", change.NewValue.GetValueOrDefault<bool>());
		}

		if (change.Property == IsSelectedProperty)
		{
			UpdatePseudoClass(":selected", change.NewValue.GetValueOrDefault<bool>());
		}
	}

	private void UpdateIndicatorOrientationPseudoClasses(Orientation orientation)
	{
		PseudoClasses.Set(":horizontal", orientation == Orientation.Horizontal);
		PseudoClasses.Set(":vertical", orientation == Orientation.Vertical);
	}

	private void UpdatePseudoClass(string pseudoClass, bool value)
	{
		PseudoClasses.Set(pseudoClass, value);
	}
}
