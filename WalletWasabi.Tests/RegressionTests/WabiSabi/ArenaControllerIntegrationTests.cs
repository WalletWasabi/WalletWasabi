using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Models;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public class ArenaControllerIntegrationTests : IntegrationTest
	{
		private readonly ArenaApiApplicationFactory _arenaApiApplicationFactory;

		public ArenaControllerIntegrationTests(ArenaApiApplicationFactory arenaApiApplicationFactory)
			: base(arenaApiApplicationFactory)
		{
			_arenaApiApplicationFactory = arenaApiApplicationFactory;
		}

		[Fact]
		public async void RegisterSpentOrInexistentCoin()
		{
			var round = _arenaApiApplicationFactory.GetCurrentRound();

			// If an output is not in the utxo dataset then it is not unspent, this
			// means that the output is spent or simply doesn't even exist.
			var nonExistingOutPoint = new OutPoint();
			using var signingKey = new Key(); // 
			var arenaClient = _arenaApiApplicationFactory.CreateArenaClient();

			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>( async () => 
				await arenaClient.RegisterInputAsync(Money.Coins(1), nonExistingOutPoint, signingKey, round.Id, round.Hash));

			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);
		}
	}
}
