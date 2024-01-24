using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class AnimatedButton : TemplatedControl
{
	public static readonly StyledProperty<ICommand> CommandProperty =
		AvaloniaProperty.Register<AnimatedButton, ICommand>(nameof(Command));

	public static readonly StyledProperty<Geometry> NormalIconProperty =
		AvaloniaProperty.Register<AnimatedButton, Geometry>(nameof(NormalIcon));

	public static readonly StyledProperty<Geometry> ClickIconProperty =
		AvaloniaProperty.Register<AnimatedButton, Geometry>(nameof(ClickIcon));

	public static readonly StyledProperty<object> CommandParameterProperty =
		AvaloniaProperty.Register<AnimatedButton, object>(nameof(CommandParameter));

	public static readonly StyledProperty<double> InitialOpacityProperty =
		AvaloniaProperty.Register<AnimatedButton, double>(nameof(InitialOpacity), 0.6);

	public static readonly StyledProperty<double> PointerOverOpacityProperty =
		AvaloniaProperty.Register<AnimatedButton, double>(nameof(PointerOverOpacity), 1);

	public static readonly StyledProperty<bool> AnimateIconProperty =
		AvaloniaProperty.Register<AnimatedButton, bool>(nameof(AnimateIcon));

	static AnimatedButton()
	{
		AffectsRender<AnimatedButton>(InitialOpacityProperty);
	}

	public ICommand Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public object CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	public Geometry NormalIcon
	{
		get => GetValue(NormalIconProperty);
		set => SetValue(NormalIconProperty, value);
	}

	public Geometry ClickIcon
	{
		get => GetValue(ClickIconProperty);
		set => SetValue(ClickIconProperty, value);
	}

	public double InitialOpacity
	{
		get => GetValue(InitialOpacityProperty);
		set => SetValue(InitialOpacityProperty, value);
	}

	public double PointerOverOpacity
	{
		get => GetValue(PointerOverOpacityProperty);
		set => SetValue(PointerOverOpacityProperty, value);
	}

	public bool AnimateIcon
	{
		get => GetValue(AnimateIconProperty);
		set => SetValue(AnimateIconProperty, value);
	}
}
