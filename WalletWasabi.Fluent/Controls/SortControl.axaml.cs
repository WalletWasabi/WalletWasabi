using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public class SortControl : TemplatedControl
{
	public static readonly StyledProperty<IEnumerable<SortableItem>> SortablesProperty = AvaloniaProperty.Register<SortControl, IEnumerable<SortableItem>>(nameof(Sortables));

	public IEnumerable<SortableItem> Sortables
	{
		get => GetValue(SortablesProperty);
		set => SetValue(SortablesProperty, value);
	}
}
