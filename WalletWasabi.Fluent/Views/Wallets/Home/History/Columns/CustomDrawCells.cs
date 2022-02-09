using Avalonia.Controls.Models.TreeDataGrid;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using System.Collections.Generic;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Views.Wallets.Home.History.Columns;

internal class PrivacyTextCell : ICell
{
	public PrivacyTextCell(string? value) => Value = value;
	public bool CanEdit => false;
	public string? Value { get; }
	object? ICell.Value => Value;
}

internal class PrivacyTextColumn : ColumnBase<HistoryItemViewModelBase>
{
	private readonly Func<HistoryItemViewModelBase, string?> _getter;

	public PrivacyTextColumn(
		object? header,
		Func<HistoryItemViewModelBase, string?> getter,
		GridLength? width,
		ColumnOptions<HistoryItemViewModelBase>? options)
		: base(header, width, options)
	{
		_getter = getter;
	}

	public override ICell CreateCell(IRow<HistoryItemViewModelBase> row)
	{
		return new PrivacyTextCell(_getter(row.Model));
	}

	public override Comparison<HistoryItemViewModelBase?>? GetComparison(ListSortDirection direction)
	{
		// TODO
		return null;
	}
}

internal class TreeDataGridPrivacyTextCell : TreeDataGridCell
{
	private static List<TreeDataGridPrivacyTextCell> Realized = new List<TreeDataGridPrivacyTextCell>();
	private static IDisposable? Subscription;
	private static bool IsContentVisible = true;
	private string? _value;
	private FormattedText? _formattedText;

	public string? Text => IsContentVisible ? _value : new string('#', _value?.Length ?? 0);

	public override void Realize(IElementFactory factory, ICell model, int columnIndex, int rowIndex)
	{
		var text = ((PrivacyTextCell)model).Value;

		if (text != _value)
		{
			_value = text;
			_formattedText = null;
		}

		base.Realize(factory, model, columnIndex, rowIndex);
	}

	public override void Render(DrawingContext context)
	{
		if (_formattedText is not null)
		{
			var r = Bounds.CenterRect(_formattedText.Bounds);
			context.DrawText(Foreground, new Point(0, r.Position.Y), _formattedText);
		}
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (Realized.Count == 0)
		{
			Subscription = Services.UiConfig
				.WhenAnyValue(x => x.PrivacyMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => SetContentVisible(!x));
		}

		Realized.Add(this);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		Realized.Remove(this);

		if (Realized.Count == 0)
		{
			Subscription?.Dispose();
			Subscription = null;
		}
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrWhiteSpace(Text))
		{
			return default;
		}

		if (availableSize != _formattedText?.Constraint)
		{
			_formattedText = new FormattedText(
				Text,
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize,
				TextAlignment.Left,
				TextWrapping.NoWrap,
				availableSize);
		}

		return _formattedText.Bounds.Size;
	}

	private static void SetContentVisible(bool value)
	{
		IsContentVisible = value;

		foreach (var c in Realized)
		{
			c._formattedText = null;
			c.InvalidateMeasure();
		}
	}
}

internal class HistoryElementFactory : TreeDataGridElementFactory
{
	protected override IControl CreateElement(object? data)
	{
		return data is PrivacyTextCell ?
			new TreeDataGridPrivacyTextCell() :
			base.CreateElement(data);
	}

	protected override string GetDataRecycleKey(object data)
	{
		return data is PrivacyTextCell ?
			typeof(TreeDataGridPrivacyTextCell).FullName! :
			base.GetDataRecycleKey(data);
	}
}

