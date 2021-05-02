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
					( new[] { new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { 1L, 1L }, new[] { -3L, -3L } }, 6 ), // TODO 5
					( new[] { new[] { 3L, 3L }, new[] { -1L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 6 ), // TODO 5
					( new[] { new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { 1L, 0L }, new[] { -3L, 0L } }, 5 ),
					( new[] { new[] { 3L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 5 ),
					( new[] { new[] { 1L, 5L }, new[] { 1L, 5L }, new[] { 1L, 5L }, new[] { -3L, -1L } }, 5 ),
					( new[] { new[] { 3L, 5L }, new[] { -1L, -1L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 7 ), // TODO 5?
					( new[] { new[] { 3L, 5L }, new[] { -1L, -1L }, new[] { -1L, -1L } }, 5 ), // TODO 4?
					( new[] { new[] { 4L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 7 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 4 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 6 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 8 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 10 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 12 ),
					( new[] { new[] { 10L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L }, new[] { -1L, 0L } }, 19 ),
					( new[] { new[] { 3L, 0L }, new[] { 3L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L } }, 6 ), // TODO 5 maxCount too pessimal?
					( new[] { new[] { 5L, 0L }, new[] { 1L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L } }, 7 ), // TODO 6 maxCount too pessimal?
					( new[] { new[] { 2L, 0L }, new[] { 2L, 0L }, new[] { -3L, 0L } }, 4 ), // TODO 3
					( new[] { new[] { 3L, 0L }, new[] { 3L, 0L }, new[] { -2L, 0L }, new[] { -2L, 0L }, new[] { -1L, 0L } }, 7 ),
					( new[] { new[] { 8L, 0L }, new[] { 1L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L } }, 7 ),
					( new[] { new[] { 8L, 0L }, new[] { 2L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L }, new[] { -3L, 0L } }, 7 ),
				}
			)
			{
				var g = DependencyGraph.ResolveCredentialDependencies(amounts);
				Assert.Equal(finalVertexCount, g.Vertices.Count);

				// TODO assertions for proper edge values
				// TODO opportunistic draining of weight credentials - add tests for max depth?
				// TODO assert max depth
			}
		}

		[Fact]
		public void ResolveCredentialDependenciesThrows()
		{
			foreach (var test in new long[][][] {
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
					new[] { new[] { 1L, 0L, 0L }, new[] { -1L, 0L, 0L } } })
			{
				Assert.Throws<ArgumentException>(() => DependencyGraph.ResolveCredentialDependencies(test));
			}

			// TODO manually construct bad graphs and ensure assert rejects them?
		}
	}
}
