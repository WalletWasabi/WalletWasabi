using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public class HistoryPlaceholderPanel : TemplatedControl
{
	private ItemsControl? _targetItemsControl;

	public double RowHeight { get; set; } = 36.5;

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		_targetItemsControl = e.NameScope.Find<ItemsControl>("PART_DummyRows");
		InvalidateMeasure();
		base.OnApplyTemplate(e);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (_targetItemsControl is null)
		{
			return availableSize;
		}

		var totalRows = (int)Math.Floor(Math.Max(1, availableSize.Height / RowHeight));

		var deltaOpacity = 1d / totalRows;

		_targetItemsControl.ItemsSource =
			Enumerable
				.Range(1, totalRows)
				.Reverse()
				.Select(mult => mult * deltaOpacity)
				.ToList();

		return base.MeasureOverride(availableSize);
	}
}
