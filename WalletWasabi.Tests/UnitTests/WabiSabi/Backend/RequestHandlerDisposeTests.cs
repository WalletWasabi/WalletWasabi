using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class RequestHandlerDisposeTests
	{
		[Fact]
		public async Task DisposeGuardAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);
			PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			await handler.DisposeAsync();
			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await handler.RegisterInputAsync(null!));

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
