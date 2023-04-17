using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public abstract class TagsBoxItemsPresenter : TemplatedControl
{
	private IEnumerable<string>? _items;

	public static readonly DirectProperty<TagsBoxItemsPresenter, IEnumerable<string>?> ItemsProperty =
		AvaloniaProperty.RegisterDirect<TagsBoxItemsPresenter, IEnumerable<string>?>(
			nameof(Items),
			o => o.Items,
			(o, v) => o.Items = v);

	public IEnumerable<string>? Items
	{
		get => _items;
		set => SetAndRaise(ItemsProperty, ref _items, value);
	}

	public ItemsControl? ItemsControl { get; protected set; }
}
