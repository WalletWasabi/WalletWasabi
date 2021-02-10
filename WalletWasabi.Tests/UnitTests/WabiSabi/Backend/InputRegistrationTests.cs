using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class InputRegistrationTests
	{
		[Fact]
		public async Task RoundNotFoundAsync()
		{
			var rc = new MockRoundCollection();
			rc.OnTryGetRound = (roundId) => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), rc);
			var req = new InputsRegistrationRequest(
				Guid.NewGuid(),
				null!,
				null!,
				null!);
			Assert.Throws<InvalidOperationException>(() => handler.RegisterInput(req));
		}
	}
}
