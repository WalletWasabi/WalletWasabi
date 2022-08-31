using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Model;

public class TreeNode : TreeNode<object>
{
	public TreeNode(object value, IEnumerable<TreeNode> children) : base(value, children)
	{
	}

	public TreeNode(object value) : base(value)
	{
	}

	public new IEnumerable<TreeNode> Children => base.Children.Cast<TreeNode>();
}

public class TreeNode<T> : ViewModelBase
{
	public TreeNode(T value, IEnumerable<TreeNode<T>> children)
	{
		Value = value;
		Children = children;
	}

	public TreeNode(T value) : this(value, Enumerable.Empty<TreeNode<T>>())
	{
	}

	public T Value { get; }
	public IEnumerable<TreeNode<T>> Children { get; }
}
