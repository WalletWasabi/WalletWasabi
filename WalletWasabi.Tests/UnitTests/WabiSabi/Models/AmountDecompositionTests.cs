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
			FeeRate feeRate = new(100m);
			var denoms = BaseDenominationGenerator.GenerateWithEffectiveCost(feeRate);
			Assert.Equal(157, denoms.Count());

			// It should be ordered.
			Assert.Equal(denoms.OrderBy(d => d), denoms);
		}
	}
}
