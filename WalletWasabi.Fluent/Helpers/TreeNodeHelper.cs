using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers;

public static class TreeNodeHelper
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
