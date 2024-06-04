using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

/// <summary>
/// Container for NavBarItems.
/// </summary>
[PseudoClasses(":horizontal", ":vertical", ":selected")]
public class NavBarItem : ContentControl
{
	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<NavBarItem, ICommand?>(nameof(Command));

	public static readonly StyledProperty<IconElement> IconProperty =
		AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

	public static readonly StyledProperty<Orientation> IndicatorOrientationProperty =
		AvaloniaProperty.Register<NavBarItem, Orientation>(nameof(IndicatorOrientation), Orientation.Vertical);

	public NavBarItem()
	{
		UpdateIndicatorOrientationPseudoClasses(IndicatorOrientation);
	}

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
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

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IndicatorOrientationProperty)
		{
			UpdateIndicatorOrientationPseudoClasses(change.GetNewValue<Orientation>());
		}
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);

		if (Command != null && Command.CanExecute(default))
		{
			Command.Execute(default);
		}
	}

	private void UpdateIndicatorOrientationPseudoClasses(Orientation orientation)
	{
		PseudoClasses.Set(":horizontal", orientation == Orientation.Horizontal);
		PseudoClasses.Set(":vertical", orientation == Orientation.Vertical);
	}
}
