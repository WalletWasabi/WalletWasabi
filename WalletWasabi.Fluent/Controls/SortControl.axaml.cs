using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class SortControl : TemplatedControl
{
	private IEnumerable<SortableItem>? _sortables;
	private IDisposable? _subscription;

	public static readonly StyledProperty<ITreeDataGridSource?> DataGridSourceProperty = AvaloniaProperty.Register<SortControl, ITreeDataGridSource?>(nameof(DataGridSource));

	public static readonly DirectProperty<SortControl, IEnumerable<SortableItem>?> SortablesProperty = AvaloniaProperty.RegisterDirect<SortControl, IEnumerable<SortableItem>?>(nameof(Sortables), o => o.Sortables, (o, v) => o.Sortables = v);

	public ITreeDataGridSource? DataGridSource
	{
		get => GetValue(DataGridSourceProperty);
		set => SetValue(DataGridSourceProperty, value);
	}

	public IEnumerable<SortableItem>? Sortables
	{
		get => _sortables;
		private set => SetAndRaise(SortablesProperty, ref _sortables, value);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_subscription = this.WhenAnyValue(x => x.DataGridSource)
			.Do(source => Sortables = Create(source))
			.Subscribe();
	}

	protected override void OnUnloaded(RoutedEventArgs e) => _subscription?.Dispose();

	private static IEnumerable<SortableItem>? Create(ITreeDataGridSource? dataSource)
	{
		return dataSource?.Columns
			.Where(IsSortable)
			.Select(
				column => new SortableItem(column.Tag?.ToString() ?? "")
				{
					SortByAscendingCommand = ReactiveCommand.Create(() => dataSource.SortBy(column, ListSortDirection.Ascending)),
					SortByDescendingCommand = ReactiveCommand.Create(() => dataSource.SortBy(column, ListSortDirection.Descending))
				});
	}

	private static bool IsSortable(object column)
	{
		// If it's an expandable column, the column we want to evaluate is in Inner.
		if (column.GetType().GetGenericTypeDefinition() == typeof(HierarchicalExpanderColumn<>))
		{
			var val = column.GetType().GetProperty("Inner")?.GetValue(column);
			if (val is null)
			{
				return false;	// If the Inner column is null, it means it doesn't have it. So, it's not Sortable.
			}
			
			column = val;
		}
		var currentType = column.GetType();
		
		// We climb the inheritance hierarchy until we find a match with ColumnBase<T>
		while (currentType is { IsGenericType: false } || currentType.GetGenericTypeDefinition() != typeof(ColumnBase<>))
		{
			currentType = currentType.BaseType;
			if (currentType == null) 
			{
				return false; // We have reached the top of the hierarchy and found no ColumnBase<T>
			}
		}
		
		// Once we found our ColumnBase<T>, we to gather info from its ColumnOptions.
		var optionsProperty = currentType.GetProperty("Options");
		var optionsValue = optionsProperty?.GetValue(column);

		if (optionsValue == null)
		{
			return false; // It's not a ColumnBase<T> or the property is not accessible.
		}

		var canUserSortColumnProperty = optionsValue.GetType().GetProperty("CanUserSortColumn");
		return (bool)(canUserSortColumnProperty?.GetValue(optionsValue) ?? false);
	}
}
