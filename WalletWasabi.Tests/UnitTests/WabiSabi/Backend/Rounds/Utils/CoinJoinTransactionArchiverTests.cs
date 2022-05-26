using NBitcoin;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using Xunit;
using static WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage.CoinJoinTransactionArchiver;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;

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

		DateTimeOffset now = DateTimeOffset.Parse("2021-09-28T20:45:30.3124Z");
		string storagePath = await archiver.StoreJsonAsync(randomTx, now);

		TransactionInfo transactionInfo = JsonSerializer.Deserialize<TransactionInfo>(File.ReadAllText(storagePath))!;
		Assert.NotNull(transactionInfo);
		Assert.Equal(1632861930312, transactionInfo.Created);
		Assert.Equal(randomTx.GetHash().ToString(), transactionInfo.TxHash);
		Assert.Equal(randomTx.ToHex(), transactionInfo.RawTransaction);
	}
}
