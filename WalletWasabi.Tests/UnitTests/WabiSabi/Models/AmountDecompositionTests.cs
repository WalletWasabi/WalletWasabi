using NBitcoin;
using System.Linq;
using WalletWasabi.WabiSabi.Models.Decomposition;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models
{
	public class AmountDecompositionTests
	{
		[Fact]
		public void BaseDenomGeneratorTest()
		{
			var denoms = BaseDenominationGenerator.Generate();
			Assert.Equal(157, denoms.Count());
			Assert.Equal(denoms, denoms.Distinct());

			// It should be ordered.
			Assert.Equal(denoms.OrderBy(d => d), denoms);
		}
	}
}
