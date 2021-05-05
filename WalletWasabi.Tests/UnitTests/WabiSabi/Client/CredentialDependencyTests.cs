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
				(var amounts, var finalVertexCount) in new (long[][], int)[]
				{
					( new[] { new[] { 1L, 1L }, new[] { -1L, -1L } }, 2 ),
					( new[] { new[] { 1L, 0L }, new[] { -1L, 0L } }, 2 ),
					( new[] { new[] { 2L, 0L }, new[] { -1L, 0L } }, 2 ),
					( new[] { new[] { 2L, 2L }, new[] { -1L, -1L } }, 2 ),
					( new[] { new[] { 2L, 2L }, new[] { -1L, -2L } }, 2 ),
					( new[] { new[] { 2L, 2L }, new[] { -2L, -2L } }, 2 ),
					( new[] { new[] { 2L, 2L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 3 ),
					( new[] { new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { -2L, -2L } }, 3 ),
					( new[] { new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { -3L, -3L } }, 5 ),
					( new[] { new[] { 3L, 3L }, new[] { -1L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 5 ),
					( new[] { new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { -3L, 0L } }, 5 ),
					( new[] { new[] { 3L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 5 ),
					( new[] { new[] { 1L, 5L }, new[] { 1L, 5L }, new[] { 1L, 5L }, new[] { -3L, -1L } }, 5 ),
					( new[] { new[] { 3L, 5L }, new[] { -1L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 6 ), // TODO 5?
					( new[] { new[] { 3L, 5L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 4 ),
					( new[] { new[] { 4L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 7 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 4 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 6 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 8 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 10 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 12 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 19 ),
					( new[] { new[] { 3L, 0L }, new[] { 3L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L } }, 5 ),
					( new[] { new[] { 5L, 0L }, new[] { 1L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L } }, 6 ),
					( new[] { new[] { 2L, 0L }, new[] { 2L, 0L }, new[] { -3L, 0L } }, 3 ),
					( new[] { new[] { 3L, 0L }, new[] { 3L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -1L, 0L } }, 6 ),
					( new[] { new[] { 8L, 0L }, new[] { 1L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L } }, 6 ),
					( new[] { new[] { 8L, 0L }, new[] { 2L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L } }, 6 ),
					( new[] { new[] { 3L, 0L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, }, 7 ),
					( new[] { new[] { 3L, 1L }, new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 7 ),
					( new[] { new[] { 3L, 3L }, new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 8 ), // TODO 7?
					( new[] { new[] { 3L, 2L }, new[] { 1L, 0L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 4 ),
					( new[] { new[] { 3L, 6L }, new[] { 1L, 0L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 5 ),
					( new[] { new[] { 3L, 6L }, new[] { 1L, 6L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 5 ), // TODO 4
					( new[] { new[] { 3L, 3L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 8 ),
					( new[] { new[] { 3L, 6L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 9 ),
					( new[] { new[] { 2L, 6L }, new[] { 2L, 6L }, new[] { -1L, -3L }, new[] { -1L, -3L }, new[] { -1L, -3L }, new[] { -1L, -3L }, }, 6 ),
					( new[] { new[] { 2L, 6L }, new[] { 2L, 6L }, new[] { -1L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L }, }, 9 ), // TODO 8
					( new[] { new[] { 3L, 6L }, new[] { 1L, 6L }, new[] { -4L, -1L }, }, 3 ),
					( new[] { new[] { 3L, 6L }, new[] { 1L, 6L }, new[] { -2L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 7 ), // TODO 5
					( new[] { new[] { 3L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -1L, -1L }, }, 9 ),
					( new[] { new[] { 3L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L }, }, 11 ), // TODO 8
					( new[] { new[] { 3L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 9 ),
					( new[] { new[] { 5L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { 1L, 6L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, new[] { -2L, -1L }, }, 15 ),
				}
			)
			{
				var g = DependencyGraph.ResolveCredentialDependencies(amounts);
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
			foreach (var test in new long[][][]
			{
				new[] { new[] { -1L, 0L } },
				new[] { new[] { 1L, 0L }, new[] { -2L, 0L } },
				new[] { new[] { 1L, 0L }, new[] { -1L, -1L } },
				new[] { new[] { 1L, 1L }, new[] { 1L, -1L } },
				new[] { new[] { 1L, -1L }, new[] { 1L, 1L } },
				new[] { new[] { 1L, -1L }, new[] { 1L, -1L } },
				new[] { new long[0], new[] { -1L, 0L } },
				new[] { new[] { 1L }, new[] { -1L, 0L } },
				new[] { new[] { 1L, 1L, 1L, }, new[] { -1L, 0L } },
				new[] { new[] { 1L, 0L }, new[] { -1L } },
				new[] { new[] { 1L, 0L }, new[] { -1L, 0L, 0L } },
				new[] { new[] { 1L, 0L, 0L }, new[] { -1L, 0L, 0L } },
			})
			{
				Assert.Throws<ArgumentException>(() => DependencyGraph.ResolveCredentialDependencies(test));
			}
		}
	}
}
