using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
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
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();
			ArenaRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			await handler.DisposeAsync();
			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await handler.RegisterInputAsync(null!));

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
