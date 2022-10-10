using System.Collections.Generic;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Extensions;

public static class TreeNodeExtensions
{
	public static IEnumerable<TreeNode<T, IEnumerable<int>>> ToTreeNodes<T>(this IEnumerable<T> enumerable, Func<T, IEnumerable<T>> getChildren)
	{
		return TreeNode.Create(enumerable, null, getChildren);
	}
}
