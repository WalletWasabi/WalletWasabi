using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class EnumerableExtensions
{
	/// <summary>
	///     Flattens a tree-like structure.
	/// </summary>
	/// <typeparam name="T">Type of the nodes.</typeparam>
	/// <param name="nodes">Root nodes.</param>
	/// <param name="getChildren">The children selector.</param>
	/// <returns>An enumerable with the flattened elements.</returns>
	public static IEnumerable<T> Flatten<T>(this IEnumerable<T> nodes, Func<T, IEnumerable<T>> getChildren)
	{
		return nodes.SelectMany(node => Flatten(node, getChildren));
	}

	/// <summary>
	///     Flattens a tree-like structure.
	/// </summary>
	/// <typeparam name="T">Type of the nodes.</typeparam>
	/// <param name="node">Root node.</param>
	/// <param name="getChildren">The children selector.</param>
	/// <returns>An enumerable with the flattened elements.</returns>
	public static IEnumerable<T> Flatten<T>(this T node, Func<T, IEnumerable<T>> getChildren)
	{
		return new[] { node }.Concat(getChildren(node).SelectMany(x => Flatten(x, getChildren)));
	}
}
