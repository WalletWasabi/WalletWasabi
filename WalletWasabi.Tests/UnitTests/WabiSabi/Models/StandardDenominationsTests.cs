using System.Linq;
using WalletWasabi.WabiSabi.Models.Decomposition;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models
{
	public class StandardDenominationsTests
	{
		[Fact]
		public void BaseDenomGeneratorTest()
		{
			var denoms = StandardDenomination.Values;
			Assert.Equal(129, denoms.Count());
			Assert.Equal(denoms, denoms.Distinct());

			// It should be ordered.
			Assert.Equal(denoms.OrderBy(d => d), denoms);
		}
	}
}
