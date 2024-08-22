using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Tests.Helpers;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

public static class IWebHostBuilderExtensions
{
	public static IWebHostBuilder AddMockRpcClient(this IWebHostBuilder builder, SmartCoin[] coins, Action<MockRpcClient> options)
	{
		var rpc = BitcoinFactory.GetMockMinimalRpc();
		rpc.Network = Network.Main;

		// Make the coordinator believe that the coins are real and
		// that they exist in the blockchain with many confirmations.
		rpc.OnGetTxOutAsync = (txId, idx, _) => new()
		{
			Confirmations = 101,
			IsCoinBase = false,
			ScriptPubKeyType = "witness_v0_keyhash",
			TxOut = coins.Single(x => x.TransactionId == txId && x.Index == idx).TxOut
		};

		rpc.OnGetRawTransactionAsync = (txid, throwIfNotFound) =>
		{
			var tx = coins.First(coin => coin.TransactionId == txid)?.Transaction?.Transaction;

			if (tx is null)
			{
				return Task.FromException<Transaction>(new InvalidOperationException("tx not found"));
			}

			return Task.FromResult(tx);
		};

		options(rpc);

		builder.ConfigureServices(services =>

			// Instruct the coordinator DI container to use these two scoped
			// services to build everything (WabiSabi controller, arena, etc)
			services.AddSingleton<IRPCClient>(s => rpc));
		return builder;
	}
}
