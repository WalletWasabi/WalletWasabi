using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class LabelsListBox : TemplatedControl, IStyleable
{
	Type IStyleable.StyleKey => typeof(LabelsListBox);

	public static readonly DirectProperty<LabelsListBox, IEnumerable> ItemsProperty =
		AvaloniaProperty.RegisterDirect<LabelsListBox, IEnumerable>(nameof(Items), o => o.Items, (o, v) => o.Items = v);

	private IEnumerable _items = new AvaloniaList<object>();

	[Content]
	public IEnumerable Items
	{
		get { return _items; }
		set { SetAndRaise(ItemsProperty, ref _items, value); }
	}
}
