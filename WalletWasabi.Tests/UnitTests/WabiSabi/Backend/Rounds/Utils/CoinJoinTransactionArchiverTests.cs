using NBitcoin;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds.Utils;
using Xunit;
using static WalletWasabi.WabiSabi.Backend.Rounds.Utils.CoinJoinTransactionArchiver;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	/// <summary>
	/// Tests for <see cref="CoinJoinTransactionArchiver"/>
	/// </summary>
	public class CoinJoinTransactionArchiverTests
	{
		[Fact]
		public async Task StoreTransactionAsync()
		{
			string tempFolder = Path.GetTempPath();
			CoinJoinTransactionArchiver archiver = new(tempFolder);

			Transaction randomTx = Network.TestNet.Consensus.ConsensusFactory.CreateTransaction();

			DateTimeOffset now = DateTimeOffset.UtcNow;
			string storagePath = await archiver.StoreJsonAsync(randomTx, now);

			TransactionInfo transactionInfo = JsonSerializer.Deserialize<TransactionInfo>(File.ReadAllText(storagePath))!;
			Assert.NotNull(transactionInfo);
			Assert.Equal(now.ToUnixTimeMilliseconds(), transactionInfo.Created);
			Assert.Equal(randomTx.GetHash().ToString(), transactionInfo.TxHash);
			Assert.Equal(randomTx.ToHex(), transactionInfo.RawTransaction);
		}
	}
}
