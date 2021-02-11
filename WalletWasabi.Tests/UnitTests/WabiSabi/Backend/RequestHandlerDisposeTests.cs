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
	public class RequestHandlerDisposeTests
	{
		[Fact]
		public async Task DisposeGuardAsync()
		{
			PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), new MockArena());
			await handler.DisposeAsync();
			Assert.Throws<ObjectDisposedException>(() => handler.RegisterInput(null!));
		}
	}
}
