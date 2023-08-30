using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Tests.UnitTests;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests;

[Collection("RegTest collection")]
public class CreateBigWalletOnRegtest
{
	private readonly ITestOutputHelper _testOutputHelper;

	public CreateBigWalletOnRegtest(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	private async Task CreateBigWalletAsync()
	{
		string rpcUser = "test";
		string rpcPassword = "test";
		int port = 18443;
		int totalNumberTx = 5000;
		int nbForeignInputOutputPerTx = 250;

		Random rnd = new();
		Network network = Network.RegTest;


		var rpcClient = new RPCClient($"{rpcUser}:{rpcPassword}",$"localhost:{port}", network);
		var mnemonicBigWallet = new Mnemonic(Wordlist.English, WordCount.Twelve); // Generate a 12-word mnemonic phrase
		_testOutputHelper.WriteLine($"Mnemonic: {string.Join(" ", mnemonicBigWallet.Words)}");
		TestWallet bigWallet = new TestWallet("BigWallet", mnemonicBigWallet, rpcClient);

		TestWallet minerWallet = new TestWallet("MinerWallet", rpcClient);
		for (var i=0; i<nbForeignInputOutputPerTx; i++)
		{
			await minerWallet.GenerateAsync(1, CancellationToken.None);
		}

		await rpcClient.GenerateToAddressAsync(101, minerWallet.ScriptPubKeys.First().Value.GetPublicKey().GetAddress(ScriptPubKeyType.Segwit, network), CancellationToken.None);

		for (var i = 0; i < totalNumberTx; i++)
		{
			var bigWalletOutputs = bigWallet.GetNextDestinations(rnd.Next(2, 8), false);
			var txid = await minerWallet.CreateSweepToManyTransactionAsync(outputsOwn: minerWallet.ScriptPubKeys.Select(x => x.Key), outputsForeign: bigWalletOutputs.Select(x => x.ScriptPubKey));
			await rpcClient.GenerateToAddressAsync(1, minerWallet.ScriptPubKeys.First().Value.GetPublicKey().GetAddress(ScriptPubKeyType.Segwit, network), CancellationToken.None);
			if (i % 100 == 0)
			{
				_testOutputHelper.WriteLine($"Progress: {i}/{totalNumberTx}");
			}
		}
	}
}
