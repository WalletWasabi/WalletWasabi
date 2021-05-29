using System;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class HideDataGridColumnBehavior : DisposingBehavior<DataGrid>
	{
		public static readonly StyledProperty<double> HideThresholdWidthProperty =
			AvaloniaProperty.Register<HideDataGridColumnBehavior, double>(nameof(HideThresholdWidth));

		public static readonly StyledProperty<string> ColumnHeaderProperty =
			AvaloniaProperty.Register<HideDataGridColumnBehavior, string>(nameof(ColumnHeader));

		public double HideThresholdWidth
		{
			get => GetValue(HideThresholdWidthProperty);
			set => SetValue(HideThresholdWidthProperty, value);
		}

		public string ColumnHeader
		{
			get => GetValue(ColumnHeaderProperty);
			set => SetValue(ColumnHeaderProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			AssociatedObject?
				.WhenAnyValue(x => x.Bounds.Width)
				.Subscribe(width =>
				{
					var column = AssociatedObject.Columns.FirstOrDefault(x => ReferenceEquals(x.Header, ColumnHeader));

					if (column is { })
					{
						column.IsVisible = width > HideThresholdWidth;
					}
				})
				.DisposeWith(disposables);
		}
	}
}
