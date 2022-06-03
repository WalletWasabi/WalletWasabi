using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class CustomElementFactory : TreeDataGridElementFactory
{
	protected override IControl CreateElement(object? data)
	{
		return data is DiscreetTextCell ?
			new TreeDataGridDiscreetTextCell() :
			base.CreateElement(data);
	}

	protected override string GetDataRecycleKey(object data)
	{
		return data is DiscreetTextCell ?
			typeof(TreeDataGridDiscreetTextCell).FullName! :
			base.GetDataRecycleKey(data);
	}
}
