using Avalonia;
using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

public class UICommand : AvaloniaObject, IUICommand
{
	public static readonly StyledProperty<string> NameProperty =
		AvaloniaProperty.Register<UICommand, string>(nameof(Name));

	public static readonly StyledProperty<object> IconProperty =
		AvaloniaProperty.Register<UICommand, object>(nameof(Icon));

	public static readonly StyledProperty<ICommand> CommandProperty =
		AvaloniaProperty.Register<UICommand, ICommand>(nameof(Command));

	public static readonly StyledProperty<bool> IsDefaultProperty =
		AvaloniaProperty.Register<UICommand, bool>(nameof(IsDefault));

	public string Name
	{
		get => GetValue(NameProperty);
		set => SetValue(NameProperty, value);
	}

	public object Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public ICommand Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public bool IsDefault
	{
		get => GetValue(IsDefaultProperty);
		set => SetValue(IsDefaultProperty, value);
	}
}
