using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class PlainTextElementFactory : TreeDataGridElementFactory
{
	protected override Control CreateElement(object? data)
	{
		return data is PlainTextCell ?
			new TreeDataGridPlainTextCell() :
			base.CreateElement(data);
	}

	protected override string GetDataRecycleKey(object? data)
	{
		return data is PlainTextCell ?
			typeof(TreeDataGridPlainTextCell).FullName! :
			base.GetDataRecycleKey(data);
	}
}
