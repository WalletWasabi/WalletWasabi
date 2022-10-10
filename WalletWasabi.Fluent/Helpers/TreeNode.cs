using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers;

public class TreeNode<T, TPath>
{
	public delegate TPath PathFactory(TreeNode<T, TPath>? previousNode, TreeNode<T, TPath> currentNode);

	private readonly PathFactory _pathFactory;

	public TreeNode(T item, int index, TreeNode<T, TPath>? parent, PathFactory pathFactory)
	{
		_pathFactory = pathFactory;
		Index = index;
		Item = item;
		Parent = parent;
	}

	public int Index { get; }
	public TPath Path => _pathFactory(Parent, this);
	public TreeNode<T, TPath>? Parent { get; }
	public T Item { get; }
	public IEnumerable<TreeNode<T, TPath>> Children { get; private set; } = null!;

	public static IEnumerable<TreeNode<T, TPath>> Create(IEnumerable<T> nodes, TreeNode<T, TPath>? parent, Func<T, IEnumerable<T>> getChildren, PathFactory pathFactory)
	{
		return nodes.Select(
			(x, i) =>
			{
				var parentNode = new TreeNode<T, TPath>(x, i, parent, pathFactory);
				var children = Create(getChildren(x), parentNode, getChildren, pathFactory);
				parentNode.Children = children;
				return parentNode;
			});
	}
}

public class TreeNode
{
	public static IEnumerable<TreeNode<T, IEnumerable<int>>> Create<T>(IEnumerable<T> nodes, TreeNode<T, IEnumerable<int>>? parent, Func<T, IEnumerable<T>> getChildren)
	{
		return TreeNode<T, IEnumerable<int>>.Create(
			nodes,
			parent,
			getChildren,
			(p, n) =>
			{
				if (p != null)
				{
					return p.Path.Concat(new[] { n.Index });
				}

				return new[] { n.Index };
			});
	}
}
