using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class HistoryPlaceholderPanel : ContentControl
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

		_targetItemsControl.Items =
			Enumerable
				.Range(1, totalRows)
				.Reverse()
				.Select(mult => mult * deltaOpacity)
				.ToList();

		return base.MeasureOverride(availableSize);
	}
}
