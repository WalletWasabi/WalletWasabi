using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.WebClients.PayJoin;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class PayjoinClientTests
	{
		[Fact]
		public void ApplyOptionalParametersTest()
		{
			var clientParameters = new PayjoinClientParameters();
			clientParameters.Version = 1;
			clientParameters.MaxAdditionalFeeContribution = new Money(50, MoneyUnit.MilliBTC);

			Uri result = PayjoinClient.ApplyOptionalParameters(new Uri("http://test.me/btc/?something=1"), clientParameters);

			// Assert that the final URI does not contain `something=1` and that it contains proper parameters (in lowercase!).
			Assert.Equal("http://test.me/btc/?v=1&disableoutputsubstitution=False&maxadditionalfeecontribution=5000000", result.AbsoluteUri);
		}
	}
}
