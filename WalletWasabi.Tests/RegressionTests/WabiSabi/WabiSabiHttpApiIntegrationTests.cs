using System.Threading;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Models;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public class WabiSabiHttpApiIntegrationTests : IntegrationTest
	{
		private readonly WabiSabiApiApplicationFactory _apiApplicationFactory;

		public WabiSabiHttpApiIntegrationTests(WabiSabiApiApplicationFactory apiApplicationFactory)
			: base(apiApplicationFactory)
		{
			_apiApplicationFactory = apiApplicationFactory;
		}

		[Fact]
		public async void RegisterSpentOrInNonExistentCoin()
		{
			var round = _apiApplicationFactory.GetCurrentRound();

			// If an output is not in the utxo dataset then it is not unspent, this
			// means that the output is spent or simply doesn't even exist.
			var nonExistingOutPoint = new OutPoint();
			using var signingKey = new Key();
			var arenaClient = _apiApplicationFactory.CreateArenaClient();

			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>( async () =>
				await arenaClient.RegisterInputAsync(Money.Coins(1), nonExistingOutPoint, signingKey, round.Id, CancellationToken.None));

			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);
		}
	}
}
