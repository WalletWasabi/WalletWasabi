using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	public static class DependencyGraphExtensions
	{
		public static IEnumerable<IEnumerable<T>> Process<T>(this DependencyGraph graph, Func<RequestNode, IEnumerable<T>, IEnumerable<T>> processFunc)
		{
			var cache = new ConcurrentDictionary<RequestNode, IEnumerable<T>>();

			IEnumerable<T> VisitNode(RequestNode node, Func<RequestNode, IEnumerable<T>, IEnumerable<T>> processFunc) =>
				cache.GetOrAdd(
					node,
					processFunc(
						node,
						Enumerable.Concat(
							graph.InEdges(node, CredentialType.Amount),
							graph.InEdges(node, CredentialType.Vsize))
						.SelectMany(e => VisitNode(e.From, processFunc))));

			return graph.Outputs.Select(output => VisitNode(output, processFunc));
		}
	}
}
