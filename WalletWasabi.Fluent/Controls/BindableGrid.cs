using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class BindableGrid : Grid
{
	public static readonly StyledProperty<string?> ColumnDefinitionsSourceProperty =
		AvaloniaProperty.Register<BindableGrid, string?>(nameof(ColumnDefinitionsSource));

	public static readonly StyledProperty<string?> RowDefinitionsSourceProperty =
		AvaloniaProperty.Register<BindableGrid, string?>(nameof(RowDefinitionsSource));

	public BindableGrid()
	{
		InvalidateDefinitions();
	}

	public string? ColumnDefinitionsSource
	{
		get => GetValue(ColumnDefinitionsSourceProperty);
		set => SetValue(ColumnDefinitionsSourceProperty, value);
	}

	public string? RowDefinitionsSource
	{
		get => GetValue(RowDefinitionsSourceProperty);
		set => SetValue(RowDefinitionsSourceProperty, value);
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == ColumnDefinitionsSourceProperty
			|| change.Property == RowDefinitionsSourceProperty)
		{
			InvalidateDefinitions();
		}
	}

	private void InvalidateDefinitions()
	{
		if (ColumnDefinitionsSource is not null && RowDefinitionsSource is not null)
		{
			var columns = GridLength.ParseLengths(ColumnDefinitionsSource).Select(x => new ColumnDefinition(x));
			ColumnDefinitions.Clear();
			ColumnDefinitions.AddRange(columns);

			var rows = GridLength.ParseLengths(RowDefinitionsSource).Select(x => new RowDefinition(x));
			RowDefinitions.Clear();
			RowDefinitions.AddRange(rows);

			InvalidateMeasure();
			InvalidateArrange();
		}
	}
}
