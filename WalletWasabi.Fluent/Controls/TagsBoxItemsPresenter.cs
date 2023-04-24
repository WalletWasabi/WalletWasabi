using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public abstract class TagsBoxItemsPresenter : TemplatedControl
{
	private IEnumerable<string>? _items;
	private ICommand? _removeTagCommand;

	public static readonly DirectProperty<TagsBoxItemsPresenter, IEnumerable<string>?> ItemsProperty =
		AvaloniaProperty.RegisterDirect<TagsBoxItemsPresenter, IEnumerable<string>?>(
			nameof(Items),
			o => o.Items,
			(o, v) => o.Items = v);

	public static readonly StyledProperty<bool> EnableCounterProperty =
		AvaloniaProperty.Register<TagsBoxItemsPresenter, bool>(nameof(EnableCounter));

	public static readonly StyledProperty<bool> EnableDeleteProperty =
		AvaloniaProperty.Register<TagsBoxItemsPresenter, bool>(nameof(EnableDelete), true);

	public static readonly DirectProperty<TagsBoxItemsPresenter, ICommand?> RemoveTagCommandProperty =
		AvaloniaProperty.RegisterDirect<TagsBoxItemsPresenter, ICommand?>(
			nameof(RemoveTagCommand),
			o => o.RemoveTagCommand,
			(o, v) => o.RemoveTagCommand = v);

	public IEnumerable<string>? Items
	{
		get => _items;
		set => SetAndRaise(ItemsProperty, ref _items, value);
	}

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

	public ICommand? RemoveTagCommand
	{
		get => _removeTagCommand;
		set => SetAndRaise(RemoveTagCommandProperty, ref _removeTagCommand, value);
	}

	public TagsBoxItemsControl? ItemsControl { get; protected set; }
}
