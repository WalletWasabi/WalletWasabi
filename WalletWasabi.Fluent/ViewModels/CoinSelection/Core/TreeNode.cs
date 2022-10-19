using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public partial class TreeNode
{
	[AutoNotify] private bool _isExpanded;

	public TreeNode(object value) : this(value, Enumerable.Empty<TreeNode>())
	{
	}

	public TreeNode(object value, IEnumerable<TreeNode> children)
	{
		Value = value;
		Children = children;
	}

	public object Value { get; }
	public IEnumerable<TreeNode> Children { get; }

	public override string? ToString()
	{
		return Value.ToString();
	}
}
