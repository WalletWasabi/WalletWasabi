using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class TagControl : ContentControl
{
	private ICommand? _removeTagCommand;

	public static readonly StyledProperty<bool> EnableCounterProperty =
		AvaloniaProperty.Register<TagControl, bool>(nameof(EnableCounter));

	public static readonly StyledProperty<bool> EnableDeleteProperty =
		AvaloniaProperty.Register<TagControl, bool>(nameof(EnableDelete));

	public static readonly StyledProperty<int> OrdinalIndexProperty =
		AvaloniaProperty.Register<TagControl, int>(nameof(OrdinalIndex));

	public static readonly DirectProperty<TagControl, ICommand?> RemoveTagCommandProperty =
		AvaloniaProperty.RegisterDirect<TagControl, ICommand?>(
			nameof(RemoveTagCommand),
			o => o.RemoveTagCommand,
			(o, v) => o.RemoveTagCommand = v);

	public bool EnableCounter
	{
		get => GetValue(EnableCounterProperty);
		set => SetValue(EnableCounterProperty, value);
	}

	public bool EnableDelete
	{
		get => GetValue(EnableDeleteProperty);
		set => SetValue(EnableDeleteProperty, value);
	}

	public int OrdinalIndex
	{
		get => GetValue(OrdinalIndexProperty);
		set => SetValue(OrdinalIndexProperty, value);
	}

	public ICommand? RemoveTagCommand
	{
		get => _removeTagCommand;
		set => SetAndRaise(RemoveTagCommandProperty, ref _removeTagCommand, value);
	}
}
