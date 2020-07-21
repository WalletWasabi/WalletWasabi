using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.Transactions.Payjoin;
using WalletWasabi.WebClients.PayJoin;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions.Payjoin
{
	/// <summary>
	/// Tests for <see cref="PayjoinClientEndpointFactory"/>.
	/// </summary>
	public class PayjoinClientEndpointFactoryTests
	{
		[Fact]
		public void ConstructEndpointUsingOptionalParametersTest()
		{
			var optionalParameters = new PayjoinClientParameters();
			optionalParameters.Version = 1;
			optionalParameters.MaxAdditionalFeeContribution = new Money(50, MoneyUnit.MilliBTC);

			var factory = new PayjoinClientEndpointFactory();
			Uri result = factory.ConstructEndpoint(new Uri("http://test.me/btc/?something=1"), optionalParameters);

			// Assert that the final URI does not contain `something=1` and that it contains proper parameters (in lowercase!).
			Assert.Equal("http://test.me/btc/?v=1&disableoutputsubstitution=false&maxadditionalfeecontribution=5000000", result.AbsoluteUri);
		}
	}
}
