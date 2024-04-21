using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class SubActionButton : ContentControl
{
	public static readonly StyledProperty<StreamGeometry> IconProperty = AvaloniaProperty.Register<SubActionButton, StreamGeometry>(nameof(Icon));

	public static readonly StyledProperty<ICommand> CommandProperty = AvaloniaProperty.Register<SubActionButton, ICommand>(nameof(Command));

	public static readonly StyledProperty<UICommandCollection> SubCommandsProperty = AvaloniaProperty.Register<SubActionButton, UICommandCollection>(nameof(SubCommands));

	public static readonly StyledProperty<IEnumerable> ItemsProperty = AvaloniaProperty.Register<SubActionButton, IEnumerable>(nameof(Items));

	public StreamGeometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public ICommand Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public UICommandCollection SubCommands
	{
		get => GetValue(SubCommandsProperty);
		set => SetValue(SubCommandsProperty, value);
	}

	public IEnumerable Items
	{
		get => GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}
}
