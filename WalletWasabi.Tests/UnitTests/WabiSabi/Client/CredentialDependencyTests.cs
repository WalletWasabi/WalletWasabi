using System;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class CredentialDependencyTests
	{
		[Fact]
		public void ResolveCredentialDependencies()
		{
			// TODO can this be done with InputData more elegantly? new() not possible in attr
			foreach (
				(var inputValues, var outputValues, var finalVertexCount) in new (ulong[][], ulong[][], int)[]
				{
					( new[] { new[] { 1UL, 1UL } }, new[] { new[] { 1UL, 1UL } }, 2 ),
					( new[] { new[] { 1UL, 0UL } }, new[] { new[] { 1UL, 0UL } }, 2 ),
					( new[] { new[] { 2UL, 0UL } }, new[] { new[] { 1UL, 0UL } }, 2 ),
					( new[] { new[] { 2UL, 2UL } }, new[] { new[] { 1UL, 1UL } }, 2 ),
					( new[] { new[] { 2UL, 2UL } }, new[] { new[] { 1UL, 2UL } }, 2 ),
					( new[] { new[] { 2UL, 2UL } }, new[] { new[] { 2UL, 2UL } }, 2 ),
					( new[] { new[] { 2UL, 2UL } }, new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, 3 ),
					( new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, new[] { new[] { 2UL, 2UL } }, 3 ),
					( new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, new[] { new[] { 3UL, 3UL } }, 5 ),
					( new[] { new[] { 3UL, 3UL } }, new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, 5 ),
					( new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, new[] { new[] { 3UL, 0UL } }, 5 ),
					( new[] { new[] { 3UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 5 ),
					( new[] { new[] { 1UL, 5UL }, new[] { 1UL, 5UL }, new[] { 1UL, 5UL } }, new[] { new[] { 3UL, 1UL } }, 5 ),
					( new[] { new[] { 3UL, 5UL } }, new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, 6 ), // TODO 5?
					( new[] { new[] { 3UL, 5UL } }, new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, 4 ),
					( new[] { new[] { 4UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 7 ),
					( new[] { new[] { 10UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 4 ),
					( new[] { new[] { 10UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 6 ),
					( new[] { new[] { 10UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 8 ),
					( new[] { new[] { 10UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 10 ),
					( new[] { new[] { 10UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 12 ),
					( new[] { new[] { 10UL, 0UL } }, new[] { new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, 19 ),
					( new[] { new[] { 3UL, 0UL }, new[] { 3UL, 0UL } }, new[] { new[] { 2UL, 0UL }, new[] { 2UL, 0UL }, new[] { 2UL, 0UL } }, 5 ),
					( new[] { new[] { 5UL, 0UL }, new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 0UL }, new[] { 2UL, 0UL }, new[] { 2UL, 0UL } }, 6 ),
					( new[] { new[] { 2UL, 0UL }, new[] { 2UL, 0UL } }, new[] { new[] { 3UL, 0UL } }, 3 ),
					( new[] { new[] { 3UL, 0UL }, new[] { 3UL, 0UL } }, new[] { new[] { 2UL, 0UL }, new[] { 2UL, 0UL }, new[] { 1UL, 0UL } }, 6 ),
					( new[] { new[] { 8UL, 0UL }, new[] { 1UL, 0UL } }, new[] { new[] { 3UL, 0UL }, new[] { 3UL, 0UL }, new[] { 3UL, 0UL } }, 6 ),
					( new[] { new[] { 8UL, 0UL }, new[] { 2UL, 0UL } }, new[] { new[] { 3UL, 0UL }, new[] { 3UL, 0UL }, new[] { 3UL, 0UL } }, 6 ),
					( new[] { new[] { 3UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 0UL }, new[] { 2UL, 0UL }, new[] { 2UL, 0UL }, }, 7 ),
					( new[] { new[] { 3UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 7 ),
					( new[] { new[] { 3UL, 3UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 8 ), // TODO 7?
					( new[] { new[] { 3UL, 2UL }, new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 4 ),
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 5 ),
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 5 ), // TODO 4
					( new[] { new[] { 3UL, 3UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 8 ),
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL }, new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 9 ),
					( new[] { new[] { 2UL, 6UL }, new[] { 2UL, 6UL } }, new[] { new[] { 1UL, 3UL }, new[] { 1UL, 3UL }, new[] { 1UL, 3UL }, new[] { 1UL, 3UL }, }, 6 ),
					( new[] { new[] { 2UL, 6UL }, new[] { 2UL, 6UL } }, new[] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, }, 9 ), // TODO 8
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 4UL, 1UL }, }, 3 ),
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 2UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } }, 7 ), // TODO 5
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 1UL, 1UL }, }, 9 ),
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, }, 11 ), // TODO 8
					( new[] { new[] { 3UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 9 ),
					( new[] { new[] { 5UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL }, new[] { 1UL, 6UL } }, new[] { new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, new[] { 2UL, 1UL }, }, 15 ),
				}
			)
			{
				var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues);
				AssertResolvedGraphInvariants(g);
				Assert.Equal(finalVertexCount, g.Vertices.Count);
			}
		}

		private void AssertResolvedGraphInvariants(DependencyGraph graph)
		{
			foreach (var node in graph.Vertices)
			{
				for (CredentialType credentialType = 0; credentialType < CredentialType.NumTypes; credentialType++)
				{
					var balance = graph.Balance(node, credentialType);

					Assert.True(balance >= 0);

					var inDegree = graph.InDegree(node, credentialType);
					Assert.InRange(inDegree, 0, DependencyGraph.K);

					var outDegree = graph.OutDegree(node, credentialType);
					Assert.InRange(outDegree, 0, DependencyGraph.K);

					if (outDegree == DependencyGraph.K)
					{
						Assert.Equal(0, balance);
					}
				}
			}

			// TODO opportunistic draining of weight credentials - add tests for max depth?
			// TODO assert max depth < ceil(log count)?
		}

		[Fact]
		public void ResolveCredentialDependenciesThrows()
		{
			foreach ((var inputValues, var outputAmounts) in new (ulong[][], ulong[][])[]
			{
				(new[] { new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 0UL } }),
				(new[] { Array.Empty<ulong>() }, new[] { new[] { 1UL, 0UL } }),
				(new[] { new[] { 1UL } }, new[] { new[] { 1UL, 0UL } }),
				(new[] { new[] { 1UL, 1UL, 1UL, } }, new[] { new[] { 1UL, 0UL } }),
				(new[] { new[] { 1UL, 0UL } }, new[] { new[] { 1UL } }),
				(new[] { new[] { 1UL, 0UL } }, new[] { new[] { 1UL, 0UL, 0UL } }),
			})
			{
				Assert.Throws<ArgumentException>(() => DependencyGraph.ResolveCredentialDependencies(inputValues, outputAmounts));
			}
		}
	}
}
