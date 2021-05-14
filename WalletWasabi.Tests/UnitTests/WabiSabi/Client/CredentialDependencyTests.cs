using System;
using System.Linq;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class CredentialDependencyTests
	{
		public void ResolveCredentialDependenciesBasic()
		{
			var g = DependencyGraph.ResolveCredentialDependencies(new ulong[][] { new[] { 1UL, 0UL } }, new ulong[][] { new[] { 1UL, 0UL } });
		}

		[Theory]
		[InlineData("1,1", "1,1", 2)]
		[InlineData("1,0", "1,0", 2)]
		[InlineData("2,0", "1,0", 2)]
		[InlineData("2,2", "1,1", 2)]
		[InlineData("2,2", "1,2", 2)]
		[InlineData("2,2", "2,2", 2)]
		[InlineData("2,2", "1,1 1,1", 3)]
		[InlineData("1,1 1,1", "2,2", 3)]
		[InlineData("1,1 1,1 1,1", "3,3", 5)]
		[InlineData("3,3", "1,1 1,1 1,1", 5)]
		[InlineData("1,0 1,0 1,0", "3,0", 5)]
		[InlineData("3,0", "1,0 1,0 1,0", 5)]
		[InlineData("1,5 1,5 1,5", "3,1", 5)]
		[InlineData("3,5", "1,1 1,1 1,1", 6)] // TODO 5?
		[InlineData("3,5", "1,1 1,1", 4)]
		[InlineData("4,0", "1,0 1,0 1,0 1,0", 7)]
		[InlineData("10,0", "1,0 1,0", 4)]
		[InlineData("10,0", "1,0 1,0 1,0", 6)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0", 8)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0", 10)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0 1,0", 12)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0", 19)]
		[InlineData("3,0 3,0", "2,0 2,0 2,0", 5)]
		[InlineData("5,0 1,0", "2,0 2,0 2,0", 6)]
		[InlineData("2,0 2,0", "3,0", 3)]
		[InlineData("3,0 3,0", "2,0 2,0 1,0", 6)]
		[InlineData("8,0 1,0", "3,0 3,0 3,0", 6)]
		[InlineData("8,0 2,0", "3,0 3,0 3,0", 6)]
		[InlineData("8,3 1,0", "3,1 3,1 3,1", 6)]
		[InlineData("8,3 2,0", "3,1 3,1 3,1", 6)]
		[InlineData("8,2 2,2", "3,1 3,1 3,1", 6)]
		[InlineData("3,0 1,0 1,0 1,0", "2,0 2,0 2,0", 7)]
		[InlineData("3,1 1,1 1,1 1,1", "2,1 2,1 2,1", 7)]
		[InlineData("3,3 1,1 1,1 1,1", "2,1 2,1 2,1", 8)] // TODO 7?
		[InlineData("3,2 1,0", "2,1 2,1", 4)]
		[InlineData("3,6 1,0", "2,1 2,1", 5)]
		[InlineData("3,6 1,6", "2,1 2,1", 5)] // TODO 4
		[InlineData("3,3 1,0 1,0 1,0", "2,1 2,1 2,1", 8)]
		[InlineData("3,6 1,0 1,0 1,0", "2,1 2,1 2,1", 9)]
		[InlineData("2,6 2,6", "1,3 1,3 1,3 1,3", 6)]
		[InlineData("2,6 2,6", "1,1 1,1 1,1 1,1", 9)] // TODO 8
		[InlineData("3,6 1,6", "4,1", 3)]
		[InlineData("3,6 1,6", "2,1 1,1 1,1", 7)] // TODO 5
		[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 1,1", 9)]
		[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 1,1 1,1", 11)] // TODO 8
		[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 2,1", 9)]
		[InlineData("5,6 1,6 1,6 1,6 1,6 1,6", "2,1 2,1 2,1 2,1 2,1", 15)]
		[InlineData("1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "21,21", 41)]
		[InlineData("21,21", "1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", 41)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,1 10,1 10,1 10,1", 43)]
		[InlineData("21,10 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,6 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 42)]
		[InlineData("21,5 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 11,6", 42)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 11,5", 41)]
		[InlineData("20,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,6", 42)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "11,5 10,5 10,5 10,6", 42)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,6", 43)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 42)]
		public void ResolveCredentialDependencies(string inputs, string outputs, int finalVertexCount)
		{
			// Parse values out of strings because InputData can't contain arrays
			var inputValues = inputs.Split(" ").Select(x => x.Split(",").Select(y => ulong.Parse(y)));
			var outputValues = outputs.Split(" ").Select(x => x.Split(",").Select(y => ulong.Parse(y)));

			var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues);
			AssertResolvedGraphInvariants(g);
			Assert.Equal(finalVertexCount, g.Vertices.Count);
		}

		private void AssertResolvedGraphInvariants(DependencyGraph graph)
		{
			for (CredentialType credentialType = 0; credentialType < CredentialType.NumTypes; credentialType++)
			{
				foreach (var node in graph.Vertices)
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
