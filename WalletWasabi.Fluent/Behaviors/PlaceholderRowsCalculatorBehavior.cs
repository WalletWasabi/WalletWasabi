using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class PlaceholderRowsCalculatorBehavior : AttachedToVisualTreeBehavior<DataBox.DataBox>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.Parent.WhenAnyValue(x => x.Bounds)
			.Select(x => x.Height)
			// .Where(x => x > 0)
			.Subscribe(CalculateRows)
			.DisposeWith(disposables);
	}

	private void CalculateRows(double height)
	{
		var totalRows = (int) Math.Floor(Math.Max(3, height / 42));
		var deltaOpacity = 1d / totalRows;

		AssociatedObject.Items =
			Enumerable
				.Range(1, totalRows)
				.Reverse()
				.Select(mult => mult * deltaOpacity)
				.ToList();
	}
}