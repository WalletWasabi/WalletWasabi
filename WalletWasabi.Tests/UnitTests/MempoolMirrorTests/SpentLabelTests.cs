using NBitcoin;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor.Http;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.MempoolMirrorTests
{
	[Collection("RegTest collection")]
	public class SpentLabelTests
	{
		public SpentLabelTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
			BackendHttpClient = regTestFixture.BackendHttpClient;
			BackendApiHttpClient = new ClearnetHttpClient(regTestFixture.HttpClient, () => RegTestFixture.BackendEndPointApiUri);
		}

		public RegTestFixture RegTestFixture { get; }
		public IHttpClient BackendHttpClient { get; }
		private IHttpClient BackendApiHttpClient { get; }

		[Fact]
		public async Task CanGetBackSpenderTransactionAsync()
		{
			(_, IRPCClient rpc, _, _, _, _, _) = await RegressionTests.Common.InitializeTestEnvironmentAsync(RegTestFixture, 0);

			var network = rpc.Network;

			using var k1 = new Key();
			var blockId = await rpc.GenerateToAddressAsync(1, k1.PubKey.WitHash.GetAddress(network));
			var block = await rpc.GetBlockAsync(blockId[0]);
			var coinBaseTx = block.Transactions[0];

			var tx = Transaction.Create(network);
			using var k2 = new Key();
			tx.Inputs.Add(coinBaseTx, 0);
			tx.Outputs.Add(Money.Coins(49.9999m), k2.PubKey.WitHash.GetAddress(network));
			tx.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());
			var valid = tx.Check();

			await rpc.GenerateAsync(101);

			using StringContent content = new($"'{tx.ToHex()}'", Encoding.UTF8, "application/json");
			using var response = await BackendApiHttpClient.SendAsync(HttpMethod.Post, "btc/blockchain/broadcast", content);

			var tx2 = Transaction.Create(network);
			using var k3 = new Key();
			tx2.Inputs.Add(coinBaseTx, 0);
			tx2.Outputs.Add(Money.Coins(45.9999m), k3.PubKey.WitHash.GetAddress(network));
			tx2.Sign(k1.GetBitcoinSecret(network), coinBaseTx.Outputs.AsCoins().First());

			using StringContent content2 = new($"'{tx2.ToHex()}'", Encoding.UTF8, "application/json");
			using var response2 = await BackendApiHttpClient.SendAsync(HttpMethod.Post, "btc/blockchain/broadcast", content2);

			var responseContent = await response2.Content.ReadAsStringAsync();
			var spenderHex = responseContent.Split(":::").ToArray()[1];

			var fixedSpenderHex = spenderHex.Remove(spenderHex.Length - 1);

			Transaction spenderTx = Transaction.Parse(fixedSpenderHex, network);

			Assert.Equal(tx.GetHash(), spenderTx.GetHash());
		}
	}
}
