using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class LabelsListBox : TemplatedControl, IStyleable
{
	public static readonly DirectProperty<LabelsListBox, IEnumerable> ItemsProperty =
		AvaloniaProperty.RegisterDirect<LabelsListBox, IEnumerable>(nameof(Items), o => o.Items, (o, v) => o.Items = v);

	private IEnumerable _items = new AvaloniaList<object>();

	Type IStyleable.StyleKey => typeof(LabelsListBox);

	[Content]
	public IEnumerable Items
	{
		get { return _items; }
		set { SetAndRaise(ItemsProperty, ref _items, value); }
	}
}
