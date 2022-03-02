using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class LabelList : TemplatedControl
{
	private IEnumerable<string>? _items;

	public static readonly StyledProperty<int> DisplayThresholdProperty =
		AvaloniaProperty.Register<LabelList, int>(nameof(DisplayThreshold), -1);

	public static readonly DirectProperty<LabelList, IEnumerable<string>?> ItemsProperty =
		AvaloniaProperty.RegisterDirect<LabelList, IEnumerable<string>?>(nameof(Items),
			o => o.Items,
			(o, v) => o.Items = v,
			enableDataValidation: false);

	public static readonly StyledProperty<IEnumerable<string>?> MainItemsProperty =
		AvaloniaProperty.Register<LabelList, IEnumerable<string>?>(nameof(MainItems));

	public static readonly StyledProperty<IEnumerable<string>?> SubItemsProperty =
		AvaloniaProperty.Register<LabelList, IEnumerable<string>?>(nameof(SubItems));

	public static readonly StyledProperty<bool> ShowSubItemsProperty =
		AvaloniaProperty.Register<LabelList, bool>(nameof(ShowSubItems));

	public LabelList()
	{
		this.WhenAnyValue(x => x.Items)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(OnItemsChanged);
	}

	private void OnItemsChanged(IEnumerable<string>? items)
	{
		if (items is null)
		{
			MainItems = null;
			SubItems = null;
			ShowSubItems = false;
			return;
		}

		var list = items.ToImmutableArray();

		if (DisplayThreshold > -1)
		{
			MainItems = list.Take(DisplayThreshold);
			SubItems = list.Skip(DisplayThreshold);
			ShowSubItems = SubItems.Any();
		}
	}

	public IEnumerable<string>? Items
	{
		get => _items;
		set => SetAndRaise(ItemsProperty, ref _items, value);
	}

	public int DisplayThreshold
	{
		get => GetValue(DisplayThresholdProperty);
		set => SetValue(DisplayThresholdProperty, value);
	}

	public IEnumerable<string>? MainItems
	{
		get => GetValue(MainItemsProperty);
		set => SetValue(MainItemsProperty, value);
	}

	public IEnumerable<string>? SubItems
	{
		get => GetValue(SubItemsProperty);
		set => SetValue(SubItemsProperty, value);
	}

	private bool ShowSubItems
	{
		get => GetValue(ShowSubItemsProperty);
		set => SetValue(ShowSubItemsProperty, value);
	}
}
